using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public class PowerProfilesViewModel : ViewModelBase
    {
        private readonly IBundledPowerProfilesService _bundledPowerProfilesService;
        private readonly IFileDialogService _fileDialogService;
        private readonly INotificationService _notificationService;

        private BundledPowerProfile _selectedProfile;
        private bool _isLoading;
        private string _searchText;
        private ObservableCollection<BundledPowerProfile> _profiles;
        private ObservableCollection<BundledPowerProfile> _filteredProfiles;

        public PowerProfilesViewModel(
            IBundledPowerProfilesService bundledPowerProfilesService,
            IFileDialogService fileDialogService,
            INotificationService notificationService)
        {
            _bundledPowerProfilesService = bundledPowerProfilesService;
            _fileDialogService = fileDialogService;
            _notificationService = notificationService;

            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshProfiles());
            ActivateProfileCommand = new RelayCommand(ActivateProfile, CanActivateProfile);
            ImportProfileCommand = new RelayCommand(_ => ImportProfile());
            ExportProfileCommand = new RelayCommand(_ => ExportProfile(), _ => CanExportProfile());
            DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => CanDeleteProfile());

            // Load profiles
            Profiles = new ObservableCollection<BundledPowerProfile>();
            FilteredProfiles = new ObservableCollection<BundledPowerProfile>();
            RefreshProfiles();
        }

        #region Properties

        public ObservableCollection<BundledPowerProfile> Profiles
        {
            get => _profiles;
            set
            {
                if (_profiles != value)
                {
                    _profiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<BundledPowerProfile> FilteredProfiles
        {
            get => _filteredProfiles;
            set
            {
                if (_filteredProfiles != value)
                {
                    _filteredProfiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public BundledPowerProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (_selectedProfile != value)
                {
                    _selectedProfile = value;
                    OnPropertyChanged();
                    InvalidateCommands();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterProfiles();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand ActivateProfileCommand { get; }
        public ICommand ImportProfileCommand { get; }
        public ICommand ExportProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }

        #endregion

        #region Command Handlers

        private void RefreshProfiles()
        {
            try
            {
                IsLoading = true;
                // Refresh profiles from service
                _bundledPowerProfilesService.RefreshProfiles();
                
                // Update our collection
                Profiles.Clear();
                foreach (var profile in _bundledPowerProfilesService.GetAllProfiles())
                {
                    Profiles.Add(profile);
                }
                
                // Apply filtering
                FilterProfiles();
                
                // If we have an active profile, select it
                var activeProfile = Profiles.FirstOrDefault(p => p.IsActive);
                if (activeProfile != null)
                {
                    SelectedProfile = activeProfile;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing profiles: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ActivateProfile(object parameter)
        {
            if (SelectedProfile == null)
                return;

            try
            {
                IsLoading = true;
                
                // If this profile is not imported yet (it's a bundled .pow file)
                if (string.IsNullOrEmpty(SelectedProfile.Guid) && !string.IsNullOrEmpty(SelectedProfile.FilePath))
                {
                    // Import it first
                    var importedProfile = _bundledPowerProfilesService.ImportProfile(SelectedProfile.FilePath);
                    if (importedProfile != null)
                    {
                        // Select the imported profile
                        SelectedProfile = Profiles.FirstOrDefault(p => p.Guid == importedProfile.Guid);
                    }
                    else
                    {
                        // Import failed
                        _notificationService.ShowError($"Failed to import the profile {SelectedProfile.Name}");
                        return;
                    }
                }
                
                // Activate the profile
                if (_bundledPowerProfilesService.ActivateProfile(SelectedProfile))
                {
                    // Update status of all profiles
                    foreach (var profile in Profiles)
                    {
                        profile.IsActive = profile.Guid == SelectedProfile.Guid;
                    }
                    
                    // Refresh the view
                    RefreshProfiles();
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error activating profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanActivateProfile(object parameter)
        {
            // Can't activate if no profile is selected
            if (SelectedProfile == null)
                return false;
            
            // Can't activate if it's already active
            if (SelectedProfile.IsActive)
                return false;
            
            return true;
        }

        private void ImportProfile()
        {
            try
            {
                // Show file dialog to pick a .pow file
                string filePath = _fileDialogService.OpenFile(
                    "Import Power Profile",
                    "Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*");
                
                if (string.IsNullOrEmpty(filePath))
                    return;
                
                IsLoading = true;
                
                // Import the profile
                var importedProfile = _bundledPowerProfilesService.ImportProfile(filePath);
                if (importedProfile != null)
                {
                    // Add to our collection if not already there
                    if (!Profiles.Any(p => p.Guid == importedProfile.Guid))
                    {
                        Profiles.Add(importedProfile);
                    }
                    
                    // Select the imported profile
                    SelectedProfile = Profiles.FirstOrDefault(p => p.Guid == importedProfile.Guid);
                    
                    // Update filtered list
                    FilterProfiles();
                    
                    _notificationService.ShowSuccess($"Successfully imported power profile: {importedProfile.Name}");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExportProfile()
        {
            if (SelectedProfile == null)
                return;

            try
            {
                // If this profile hasn't been imported yet, we can just copy the file
                if (string.IsNullOrEmpty(SelectedProfile.Guid) && !string.IsNullOrEmpty(SelectedProfile.FilePath))
                {
                    string targetPath = _fileDialogService.SaveFile(
                        "Export Power Profile",
                        "Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*",
                        null,
                        $"{SelectedProfile.Name}.pow");
                    
                    if (string.IsNullOrEmpty(targetPath))
                        return;
                    
                    // Copy the file
                    System.IO.File.Copy(SelectedProfile.FilePath, targetPath, true);
                    _notificationService.ShowSuccess($"Successfully exported power profile: {SelectedProfile.Name}");
                    return;
                }
                
                // For imported/Windows profiles, use the service
                string filePath = _fileDialogService.SaveFile(
                    "Export Power Profile",
                    "Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*",
                    null,
                    $"{SelectedProfile.Name}.pow");
                
                if (string.IsNullOrEmpty(filePath))
                    return;
                
                IsLoading = true;
                
                // Export the profile
                if (_bundledPowerProfilesService.ExportProfile(SelectedProfile, filePath))
                {
                    _notificationService.ShowSuccess($"Successfully exported power profile: {SelectedProfile.Name}");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanExportProfile()
        {
            return SelectedProfile != null;
        }

        private void DeleteProfile()
        {
            if (SelectedProfile == null)
                return;

            try
            {
                // Confirm before deleting
                if (!_notificationService.ShowConfirmation(
                    $"Are you sure you want to delete the power profile '{SelectedProfile.Name}'?",
                    "Confirm Deletion"))
                {
                    return;
                }
                
                IsLoading = true;
                
                // Delete the profile
                if (_bundledPowerProfilesService.DeleteProfile(SelectedProfile))
                {
                    // Remove from our collection
                    Profiles.Remove(SelectedProfile);
                    
                    // Update filtered list
                    FilterProfiles();
                    
                    // Clear selection
                    SelectedProfile = null;
                    
                    _notificationService.ShowSuccess($"Successfully deleted power profile.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error deleting profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanDeleteProfile()
        {
            // Can only delete non-built-in and non-active profiles
            return SelectedProfile != null && !SelectedProfile.IsBuiltIn && !SelectedProfile.IsActive;
        }

        private void FilterProfiles()
        {
            // Apply search filter
            FilteredProfiles.Clear();
            
            var query = Profiles.AsEnumerable();
            
            // Filter by search text
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string searchLower = SearchText.ToLowerInvariant();
                query = query.Where(p => 
                    p.Name.ToLowerInvariant().Contains(searchLower) || 
                    p.Description.ToLowerInvariant().Contains(searchLower) ||
                    p.Tags.ToLowerInvariant().Contains(searchLower));
            }
            
            // Order by active first, then by name
            query = query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name);
            
            foreach (var profile in query)
            {
                FilteredProfiles.Add(profile);
            }
        }

        private void InvalidateCommands()
        {
            (ActivateProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion
    }
}