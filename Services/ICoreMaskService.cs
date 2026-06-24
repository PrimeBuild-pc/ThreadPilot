namespace ThreadPilot.Services
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface ICoreMaskService
    {
        ObservableCollection<CoreMask> AvailableMasks { get; }

        CoreMask? DefaultMask { get; }

        Task InitializeAsync();

        Task<CoreMask> CreateMaskAsync(string name, string description, IEnumerable<bool> boolMask);

        Task UpdateMaskAsync(CoreMask mask);

        Task DeleteMaskAsync(string maskId);

        CoreMask? GetMaskById(string maskId);

        CoreMask? GetMaskByName(string name);

        Task SaveMasksAsync();

        Task LoadMasksAsync();

        Task<bool> IsMaskReferencedByProfilesAsync(string maskId);

        Task<bool> IsMaskActivelyAppliedAsync(string maskId);

        Task<IEnumerable<string>> GetProfilesReferencingMaskAsync(string maskId);

        Task UpdateProfilesToDefaultMaskAsync(string maskId);

        Task CreateDefaultMasksAsync();

        CoreMask? GetAllCoresMask();

        void RegisterMaskApplication(int processId, string maskId);

        void UnregisterMaskApplication(int processId);
    }
}

