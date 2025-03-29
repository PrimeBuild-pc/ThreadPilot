using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the power profiles view
    /// </summary>
    public class PowerProfilesViewModel : ViewModelBase
    {
        private readonly IPowerProfileService _powerProfileService;
        private readonly IBundledPowerProfilesService _bundledPowerProfilesService;
        private readonly IFileDialogService _fileDialogService;
        private readonly NotificationService _notificationService;
        private ObservableCollection<BundledPowerProfile> _bundledProfiles;
        private BundledPowerProfile _selectedBundledProfile;
        private bool _isLoading;
        private bool _isRefreshing;
        
        /// <summary>
        /// Gets or sets a value indicating whether the view is loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the view is refreshing
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        
        /// <summary>
        /// Gets or sets the bundled power profiles
        /// </summary>
        public ObservableCollection<BundledPowerProfile> BundledProfiles
        {
            get => _bundledProfiles;
            set => SetProperty(ref _bundledProfiles, value);
        }
        
        /// <summary>
        /// Gets or sets the selected bundled profile
        /// </summary>
        public BundledPowerProfile SelectedBundledProfile
        {
            get => _selectedBundledProfile;
            set
            {
                SetProperty(ref _selectedBundledProfile, value);
                OnPropertyChanged(nameof(IsProfileSelected));
                OnPropertyChanged(nameof(IsProfileActive));
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether a profile is selected
        /// </summary>
        public bool IsProfileSelected => SelectedBundledProfile != null;
        
        /// <summary>
        /// Gets a value indicating whether the selected profile is active
        /// </summary>
        public bool IsProfileActive => SelectedBundledProfile?.IsActive ?? false;
        
        /// <summary>
        /// Gets the command to load profiles
        /// </summary>
        public ICommand LoadProfilesCommand { get; }
        
        /// <summary>
        /// Gets the command to refresh profiles
        /// </summary>
        public ICommand RefreshProfilesCommand { get; }
        
        /// <summary>
        /// Gets the command to import a profile
        /// </summary>
        public ICommand ImportProfileCommand { get; }
        
        /// <summary>
        /// Gets the command to activate a profile
        /// </summary>
        public ICommand ActivateProfileCommand { get; }
        
        /// <summary>
        /// Constructor for PowerProfilesViewModel
        /// </summary>
        /// <param name="powerProfileService">The power profile service</param>
        /// <param name="bundledPowerProfilesService">The bundled power profiles service</param>
        /// <param name="fileDialogService">The file dialog service</param>
        /// <param name="notificationService">The notification service</param>
        public PowerProfilesViewModel(
            IPowerProfileService powerProfileService,
            IBundledPowerProfilesService bundledPowerProfilesService,
            IFileDialogService fileDialogService,
            NotificationService notificationService)
        {
            _powerProfileService = powerProfileService;
            _bundledPowerProfilesService = bundledPowerProfilesService;
            _fileDialogService = fileDialogService;
            _notificationService = notificationService;
            
            BundledProfiles = new ObservableCollection<BundledPowerProfile>();
            
            LoadProfilesCommand = new RelayCommand(async () => await LoadProfilesAsync());
            RefreshProfilesCommand = new RelayCommand(async () => await RefreshProfilesAsync());
            ImportProfileCommand = new RelayCommand(async () => await ImportProfileAsync());
            ActivateProfileCommand = new RelayCommand(async () => await ActivateProfileAsync(), 
                () => IsProfileSelected && !IsProfileActive);
        }
        
        /// <summary>
        /// Loads the power profiles
        /// </summary>
        public async Task LoadProfilesAsync()
        {
            try
            {
                IsLoading = true;
                
                BundledProfiles.Clear();
                
                var profiles = await _bundledPowerProfilesService.GetBundledProfilesAsync();
                
                foreach (var profile in profiles)
                {
                    BundledProfiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error loading profiles: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Refreshes the power profiles
        /// </summary>
        public async Task RefreshProfilesAsync()
        {
            try
            {
                IsRefreshing = true;
                
                await _bundledPowerProfilesService.RefreshProfileStatusAsync();
                
                OnPropertyChanged(nameof(IsProfileActive));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing profiles: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        
        /// <summary>
        /// Imports a power profile
        /// </summary>
        public async Task ImportProfileAsync()
        {
            try
            {
                string filePath = _fileDialogService.ShowOpenFileDialog("Select Power Profile", "Power Profile (*.pow)|*.pow");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }
                
                var profile = await _bundledPowerProfilesService.ImportExternalProfileAsync(filePath);
                
                if (profile != null)
                {
                    BundledProfiles.Add(profile);
                    SelectedBundledProfile = profile;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Activates the selected power profile
        /// </summary>
        public async Task ActivateProfileAsync()
        {
            try
            {
                if (SelectedBundledProfile == null)
                {
                    return;
                }
                
                bool result = await _bundledPowerProfilesService.ActivateProfileAsync(SelectedBundledProfile);
                
                if (result)
                {
                    await RefreshProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error activating profile: {ex.Message}");
            }
        }
    }
}