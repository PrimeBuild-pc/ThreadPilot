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
}

