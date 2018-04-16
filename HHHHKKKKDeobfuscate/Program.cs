using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Data.Sqlite;
using DocoptNet;

namespace HHHHKKKKDeobfuscate
{
    /// <summary>
    /// Apply a "self-xor" to packet data; this is for testing purposes.
    /// </summary>
    public class XorTransform
    {
        public byte[] Keys { get; set; }
        public XorTransform(byte[] keys) {
            this.Keys = keys;
        }
        public void DeobfuscateArray(ref byte[] buffer)
        {
            int keysBytePosition = (buffer.Length + Keys[0] + Keys[Keys.Length / 2]) % Keys.Length;
            for (int i = 0; i < buffer.Length; i++, keysBytePosition++)
            {
                if (keysBytePosition >= Keys.Length)
                {
                    keysBytePosition = 0;
                }
                buffer[i] ^= Keys[keysBytePosition];
            }
        }


    }

    static class Helpers
    {
        // Extension method to allow foreach
        public static IEnumerable<System.Data.IDataRecord> Enumerate(this System.Data.IDataReader reader)
        {
            while (reader.Read())
            {
                yield return reader;
            }
        }

        public static byte[] GetDeobfuscatedRefOrNull(XorTransform converter, System.Data.Common.DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }
            byte[] refernce = reader.GetFieldValue<byte[]>(ordinal);
            converter.DeobfuscateArray(ref refernce);
            return refernce;
        }

        private static object _MessageLock = new object();
        public static void WriteLine(string message, ConsoleColor color = ConsoleColor.Red)
        {
            lock (_MessageLock)
            {
                Console.BackgroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

    }

    class Program
    {
        private const string usage = @"A tool for extracting resources from HHHHKKKK.

    Usage:
      HHHHKKKKDeobfuscate.exe [--dump=<dir>] <assets_path>
      HHHHKKKKDeobfuscate.exe (-h | --help | --version)

    Options:
      -h --help     Show this help message
      --version     Show the program's version number
      --dump=<dir>  Extract script to <dir> [default: .].

    ";
        static void Main(string[] args)
        {
            var arguments = new Docopt().Apply(usage, args, version: "HHHHKKKKDeobfuscate 1.0", exit: true);
            SQLitePCL.Batteries_V2.Init();  // Reference SQLitePCLRaw.bundle_e_sqlite3 for this to work
            var assetConverter = new XorTransform(HHHHKKKKDeobfuscate.HHHHKKKK_asset_key);
            var scriptConverter = new XorTransform(HHHHKKKKDeobfuscate.HHHHKKKK_script_key);

            // Process assets
            foreach (var globext in new Dictionary<string, string>
            {
                ["*.tcp"] = ".png",
                ["*.tcj"] = ".jpg",
            })
            {
                foreach (var filePath in Directory.GetFiles(arguments["<assets_path>"].ToString(), globext.Key, SearchOption.AllDirectories))
                {
                    Console.WriteLine(filePath);
                    var mydata = System.IO.File.ReadAllBytes(filePath);
                    assetConverter.DeobfuscateArray(ref mydata);
                    File.WriteAllBytes(Path.ChangeExtension(filePath, globext.Value), mydata);
                }
            }

            // Process script db
            using (var fileStream = new FileStream(Path.Combine(arguments["<assets_path>"].ToString(), @"db", @"scr.db.org"), FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fileStream))
            {
                foreach (var filePath in Directory.GetFiles(Path.Combine(arguments["<assets_path>"].ToString(), @"db"), "scr_?.tmp"))
                {
                    Console.WriteLine(filePath);
                    var mydata = System.IO.File.ReadAllBytes(filePath);
                    assetConverter.DeobfuscateArray(ref mydata);
                    bw.Write(mydata);
                }
            }
            // working copy
            var targetDatabase = Path.Combine(arguments["<assets_path>"].ToString(), @"db", @"scr.db");
            File.Copy(Path.Combine(arguments["<assets_path>"].ToString(), @"db", @"scr.db.org"), targetDatabase, true);
            using (SqliteConnection db = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = targetDatabase,
                    Mode = SqliteOpenMode.ReadWrite
                }.ToString()))
            {
                db.Open();

                var tableReaderCmd = db.CreateCommand();
                // Note: db.GetSchema("tables") throws NotSupportedException
                tableReaderCmd.CommandText = @"SELECT name FROM sqlite_master WHERE type='table' AND name!='version' ORDER BY NAME;";
                var tableReader = tableReaderCmd.ExecuteReader();
                foreach (var tableRecord in tableReader.Enumerate())
                {
                    var thisTable = tableRecord.GetString(0);
                    // Batch queries in a single string
                    var taBlobUpdateCmd = db.CreateCommand();
                    List<string> batchTaBlobUpdateSqls = new List<string>();
                    uint counter = 0;

                    var taBlobReaderCmd = db.CreateCommand();
                    taBlobReaderCmd.CommandText = $"SELECT _id, t, ta, o FROM {thisTable} WHERE t IS NOT NULL OR ta IS NOT NULL OR o IS NOT NULL;";
                    var taBlobReader = taBlobReaderCmd.ExecuteReader();
                    foreach (System.Data.Common.DbDataReader ScriptlineRecord in taBlobReader.Enumerate())
                    {
                        var tblob = Helpers.GetDeobfuscatedRefOrNull(scriptConverter, ScriptlineRecord, 1);
                        var tablob = Helpers.GetDeobfuscatedRefOrNull(scriptConverter, ScriptlineRecord, 2);
                        var oblob = Helpers.GetDeobfuscatedRefOrNull(scriptConverter, ScriptlineRecord, 3);

                        ++counter;  // create indexed parameters
                        var tPlaceholder = String.Format("$t{0}", counter);
                        var taPlaceholder = String.Format("$ta{0}", counter);
                        var oPlaceholder = String.Format("$o{0}", counter);
                        var idPlaceholder = String.Format("$id{0}", counter);
                        batchTaBlobUpdateSqls.Add($"UPDATE {thisTable} " +
                            $"SET t={tPlaceholder}, ta={taPlaceholder}, o={oPlaceholder} " +
                            $"WHERE _id={idPlaceholder};");

                        taBlobUpdateCmd.Parameters.AddWithValue(tPlaceholder, ((object)tblob ?? DBNull.Value));
                        taBlobUpdateCmd.Parameters.AddWithValue(taPlaceholder, ((object)tablob ?? DBNull.Value));
                        taBlobUpdateCmd.Parameters.AddWithValue(oPlaceholder, ((object)oblob ?? DBNull.Value));
                        taBlobUpdateCmd.Parameters.AddWithValue(idPlaceholder, ScriptlineRecord.GetValue(0));
                    }
                    if (counter > 0 && batchTaBlobUpdateSqls.Count > 0)
                    {
                        taBlobUpdateCmd.CommandText = String.Join(" ", batchTaBlobUpdateSqls);
                        int modifiedCount = taBlobUpdateCmd.ExecuteNonQuery();
#if DEBUG
                        Helpers.WriteLine(String.Format("{0}: {1} lines modified", thisTable, modifiedCount.ToString()), ConsoleColor.Green);
#endif
                    }
                    else
                    {
#if DEBUG
                        Helpers.WriteLine(String.Format("{0}: 0 matching line", thisTable));
#endif
                    }
                }
                db.Close();
            }
        }
    }
}
