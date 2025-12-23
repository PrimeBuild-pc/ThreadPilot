/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using Microsoft.Extensions.DependencyInjection;

namespace ThreadPilot.Helpers
{
    public static class ServiceProviderExtensions
    {
        public static IServiceProvider Services => ((App)App.Current).ServiceProvider;
        
        public static T? GetService<T>() where T : class
        {
            return Services.GetService(typeof(T)) as T;
        }
    }
}
