using System;
using System.Globalization;
using System.IO;

namespace RailStudio.Services
{
    /// <summary>
    /// Enterprise-grade service for creating timestamped backups of manifest files.
    /// Follows ISO8601 timestamp format with sequence numbers for collision handling.
    /// </summary>
    public interface IManifestBackupService
    {
        /// <summary>
        /// Creates a backup of the specified manifest file.
        /// </summary>
        /// <param name="manifestPath">Path to the manifest file to backup.</param>
        /// <returns>Path to the created backup file.</returns>
        string CreateBackup(string manifestPath);
    }

    public class ManifestBackupService : IManifestBackupService
    {
        private const string BACKUP_PREFIX = "Rail.manifest.backup.";
        private const string BACKUP_EXTENSION = ".json";

        public string CreateBackup(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath))
                throw new ArgumentException("Manifest path cannot be null or empty", nameof(manifestPath));

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Manifest file not found", manifestPath);

            var directory = Path.GetDirectoryName(manifestPath) 
                ?? throw new InvalidOperationException($"Cannot determine directory for: {manifestPath}");

            // Generate ISO8601 timestamp: yyyyMMddTHHmmss
            var timestamp = DateTime.Now.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);

            // Find next available sequence number for this timestamp
            int sequence = 1;
            string backupPath;
            do
            {
                var sequenceStr = sequence.ToString("D3"); // Zero-padded to 3 digits (001, 002, ...)
                var backupFileName = $"{BACKUP_PREFIX}{timestamp}-{sequenceStr}{BACKUP_EXTENSION}";
                backupPath = Path.Combine(directory, backupFileName);
                sequence++;
            }
            while (File.Exists(backupPath));

            // Create atomic backup
            try
            {
                File.Copy(manifestPath, backupPath, overwrite: false);
                return backupPath;
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to create backup at {backupPath}", ex);
            }
        }
    }
}




