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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ThreadPilot.Helpers
{
    public static class AffinityHelper
    {
        public static long CalculateAffinityMask(IEnumerable<System.Windows.Controls.CheckBox> cpuCheckboxes)
        {
            return cpuCheckboxes
                .Where(cb => cb.IsChecked == true)
                .Sum(cb => (long)cb.Tag);
        }

        public static void UpdateCheckboxesFromMask(IEnumerable<System.Windows.Controls.CheckBox> cpuCheckboxes, long affinityMask)
        {
            foreach (var checkbox in cpuCheckboxes)
            {
                var cpuBit = (long)checkbox.Tag;
                checkbox.IsChecked = (affinityMask & cpuBit) != 0;
            }
        }
    }
}
