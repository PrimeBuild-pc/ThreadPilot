/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Models.Core
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Base interface for all domain models.
    /// </summary>
    public interface IModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets unique identifier for the model instance.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets timestamp when the model was created.
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Gets timestamp when the model was last updated.
        /// </summary>
        DateTime UpdatedAt { get; }

        /// <summary>
        /// Validate the model state.
        /// </summary>
        ValidationResult Validate();

        /// <summary>
        /// Create a copy of the model.
        /// </summary>
        IModel Clone();
    }

    /// <summary>
    /// Validation result for model validation.
    /// </summary>
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

    /// <summary>
    /// Base implementation for domain models.
    /// </summary>
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

