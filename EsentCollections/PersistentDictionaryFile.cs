﻿//-----------------------------------------------------------------------
// <copyright file="PersistentDictionaryFile.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;

namespace Microsoft.Isam.Esent.Collections.Generic
{
    /// <summary>
    /// Methods that deal with <see cref="PersistentDictionary{TKey,TValue}"/>
    /// database files.
    /// </summary>
    public static class PersistentDictionaryFile
    {
        /// <summary>
        /// Determine if a dictionary database file exists in the specified directory.
        /// </summary>
        /// <param name="directory">The directory to look in.</param>
        /// <returns>True if the database file exists, false otherwise.</returns>
        public static bool Exists(string directory)
        {
            if (null == directory)
            {
                throw new ArgumentNullException("directory");    
            }

            if (Directory.Exists(directory))
            {
                var config = new PersistentDictionaryConfig();
                var databasePath = Path.Combine(directory, config.Database);
                return File.Exists(databasePath);
            }

            return false;
        }

        /// <summary>
        /// Delete all files associated with a PersistedDictionary database from
        /// the specified directory.
        /// </summary>
        /// <param name="directory">The directory to delete the files from.</param>
        public static void DeleteFiles(string directory)
        {
            if (null == directory)
            {
                throw new ArgumentNullException("directory");
            }

            if (Directory.Exists(directory))
            {
                var config = new PersistentDictionaryConfig();
                var databasePath = Path.Combine(directory, config.Database);
                File.Delete(databasePath);
                File.Delete(Path.Combine(directory, String.Format("{0}.chk", config.BaseName)));
                foreach (string file in Directory.GetFiles(directory, String.Format("{0}*.log", config.BaseName)))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
