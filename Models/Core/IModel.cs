namespace ThreadPilot.Models.Core
{
    using System;
    using System.ComponentModel;

    public interface IModel : INotifyPropertyChanged
    {
        string Id { get; }

        DateTime CreatedAt { get; }

        DateTime UpdatedAt { get; }

        ValidationResult Validate();

        IModel Clone();
    }

    public class ValidationResult
    {
        public bool IsValid { get; }

        public string[] Errors { get; }

        public ValidationResult(bool isValid, params string[] errors)
        {
            this.IsValid = isValid;
            this.Errors = errors ?? Array.Empty<string>();
        }

        public static ValidationResult Success() => new(true);

        public static ValidationResult Failure(params string[] errors) => new(false, errors);
    }

    public abstract class BaseModel : IModel
    {
        public string Id { get; protected set; }

        public DateTime CreatedAt { get; protected set; }

        public DateTime UpdatedAt { get; protected set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected BaseModel()
        {
            this.Id = Guid.NewGuid().ToString();
            this.CreatedAt = DateTime.UtcNow;
            this.UpdatedAt = DateTime.UtcNow;
        }

        protected BaseModel(string id)
        {
            this.Id = id ?? throw new ArgumentNullException(nameof(id));
            this.CreatedAt = DateTime.UtcNow;
            this.UpdatedAt = DateTime.UtcNow;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            this.UpdatedAt = DateTime.UtcNow;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        public abstract ValidationResult Validate();

        public abstract IModel Clone();
    }
}

