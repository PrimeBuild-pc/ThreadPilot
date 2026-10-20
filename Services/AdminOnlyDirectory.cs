/*
 * ThreadPilot - directories only administrators and SYSTEM can write, for files the elevated app later runs or hands to Windows.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Security.AccessControl;
    using System.Security.Principal;

    public static class AdminOnlyDirectory
    {
        /// <summary>
        /// Gets the default parent: in %SystemRoot%\Temp users may create folders but cannot rename or delete
        /// another account's, unlike their own %TEMP%, where they could swap a file between check and use.
        /// </summary>
        public static string DefaultRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

        /// <summary>Creates <paramref name="path"/> (which must not exist) with an Administrators + SYSTEM only DACL.</summary>
        public static string Create(string path)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            Allow(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
            Allow(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));

            // Not elevated (tests, dev runs): there is no privilege gap to protect, and without this the
            // process could not write its own files.
            using var identity = WindowsIdentity.GetCurrent();
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator) && identity.User != null)
            {
                Allow(security, identity.User);
            }

            if (Directory.Exists(path) || File.Exists(path))
            {
                throw new IOException($"Refusing to reuse existing path '{path}'.");
            }

            security.CreateDirectory(path);
            return path;
        }

        private static void Allow(DirectorySecurity security, IdentityReference identity)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                identity,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }
    }
}
