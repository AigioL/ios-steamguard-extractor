using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Claunia.PropertyList;
using ios_steamguard_extractor;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace iOSSteamGuardExtractor
{
    public partial class MainWindow : Window
    {
        readonly Button btnGetSteamGuardData;
        readonly TextBox txtResults;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            btnGetSteamGuardData = this.FindControl<Button>(nameof(btnGetSteamGuardData));
            txtResults = this.FindControl<TextBox>(nameof(txtResults));
            btnGetSteamGuardData.Click += btnGetSteamGuardData_Click;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private static int SearchBytes(IReadOnlyList<byte> haystack, IReadOnlyList<byte> needle, int start)
        {
            var len = needle.Count;
            var limit = haystack.Count - len;
            for (var i = start; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }

        private bool ProcessSteamGuardFile(string filename, string filepath, string deviceID)
        {
            StringBuilder b = new(txtResults.Text);
            b.AppendFormat("Processing {0}", filename);
            b.AppendLine();
            try
            {
                var sgdata = BinaryPropertyListParser.Parse(File.ReadAllBytes(filepath));
                //var sglist = ((NSArray)((NSDictionary)sgdata)["$objects"]).GetArray();
                IList<NSObject> sglist;
                if (sgdata is NSDictionary sgdata_ && sgdata_["$objects"] is NSArray sgdata__)
                {
                    sglist = sgdata__;
                }
                else
                {
                    b.AppendLine("sgdata incorrect format.");
                    return false;
                }
                var auth = new SteamAuthenticator()
                {
                    DeviceID = $"android:{deviceID}"
                };

                for (var i = 2; i < 14; i++)
                {
                    switch (sglist[i].ToString())
                    {
                        case "shared_secret":
                            auth.SharedSecret = sglist[i + 12].ToString();
                            break;
                        case "uri":
                            auth.Uri = sglist[i + 12].ToString();
                            break;
                        case "steamid":
                            auth.Steamid = sglist[i + 12].ToString();
                            break;
                        case "revocation_code":
                            auth.RevocationCode = sglist[i + 12].ToString();
                            break;
                        case "serial_number":
                            auth.SerialNumber = sglist[i + 12].ToString();
                            break;
                        case "token_gid":
                            auth.TokenGid = sglist[i + 12].ToString();
                            break;
                        case "identity_secret":
                            auth.IdentitySecret = sglist[i + 12].ToString();
                            break;
                        case "secret_1":
                            auth.Secret = sglist[i + 12].ToString();
                            break;
                        case "server_time":
                            auth.ServerTime = sglist[i + 12].ToString();
                            break;
                        case "account_name":
                            auth.AccountName = sglist[i + 12].ToString();
                            break;
                        case "steamguard_scheme":
                            auth.SteamguardScheme = sglist[i + 12].ToString();
                            break;
                        case "status":
                            auth.Status = sglist[i + 12].ToString();
                            break;
                    }
                }

                b.AppendLine();

                b.AppendLine("In WinAuth, Add Steam Authenticator. Select the Import Android Tab");
                b.AppendLine();

                b.AppendLine("Paste this into the steam_uuid.xml text box");

                b.AppendFormat("android:{0}", deviceID);
                b.AppendLine();
                b.AppendLine();

                b.AppendLine("Paste the following data, including the {} into the SteamGuare-NNNNNNNNN... text box");

                b.AppendLine(JsonConvert.SerializeObject(auth, Formatting.Indented));
                b.AppendLine();

                b.AppendLine(JsonConvert.SerializeObject(auth, Formatting.Indented));
                b.AppendLine("Alternatively, you can paste the above json text into {botname}.maFile in your ASF config directory, if you use ASF");
            }
            catch (PropertyListFormatException) //The only way this should happen is if we opened an encrypted backup.
            {
                b.AppendLine("Error: Encrypted backups are not supported. You need to create a decrypted backup to proceed.");
                return false;
            }
            catch (Exception ex)
            {
                b.AppendFormat("An Exception occurred while processing: {0}", ex.Message);
                return false;
            }
            finally
            {
                txtResults.Text += b.ToString();
            }
            return true;
        }

        private void ProcessIOS9Backup(string d)
        {
            var guid = ProcessInfoPlist(d);
            if (guid == null) return;
            var data = File.ReadAllBytes(Path.Combine(d, "Manifest.mbdb"));
            var steamfiles = Encoding.UTF8.GetBytes("AppDomain-com.valvesoftware.Steam");
            for (var index = 0; ; index += steamfiles.Length)
            {
                index = SearchBytes(data, steamfiles, index);
                if (index == -1) break;
                var index2 = index + steamfiles.Length;
                var filelen = data[index2] << 8 | data[index2 + 1];
                var temp = new byte[filelen];
                Array.Copy(data, index2 + 2, temp, 0, filelen);
                var steamfilename = Encoding.UTF8.GetString(temp);
                if (!steamfilename.StartsWith("Documents/Steamguard-")) continue;
                var hash =
                    new SHA1Managed().ComputeHash(
                        Encoding.UTF8.GetBytes("AppDomain-com.valvesoftware.Steam-" + steamfilename));
                var hashstr = BitConverter.ToString(hash).Replace("-", "");
                if (File.Exists(Path.Combine(d, hashstr)))
                {
                    if (!ProcessSteamGuardFile(steamfilename, Path.Combine(d, hashstr), guid))
                        break;
                }
                else
                {
                    txtResults.Text += $"Error: {steamfilename} is missing from ios backup, aborting{Environment.NewLine}";
                    break;
                }
            }
        }

        private void ProcessIOS10Backup(string d)
        {
            try
            {
                var guid = ProcessInfoPlist(d);
                if (guid == null) return;
                var dbConnection = new SqliteConnection($"Data Source=\"{Path.Combine(d, "Manifest.db")}\";Version=3;");
                dbConnection.Open();
                var query =
                    "Select * from Files where domain is 'AppDomain-com.valvesoftware.Steam' and relativePath like 'Documents/Steamguard-%'";
                var dbCommand = new SqliteCommand(query, dbConnection);
                var dbReader = dbCommand.ExecuteReader();
                while (dbReader.Read())
                {
                    var startID = dbReader["fileID"].ToString().Substring(0, 2);
                    var result = ProcessSteamGuardFile(dbReader["relativePath"].ToString(),
                        Path.Combine(d, startID, dbReader["fileID"].ToString()), guid);
                    if (!result) break;
                }
                dbConnection.Close();
            }
            catch (SqliteException)
            {
                txtResults.Text += $"Error: Encrypted backups are not supported. You need to create a decrypted backup to proceed.{Environment.NewLine}";
            }
            catch (Exception ex)
            {
                txtResults.Text += $"An Exception occurred while processing: {ex.Message}";
            }
        }

        private string ProcessInfoPlist(string d)
        {
            try
            {
                var info = (NSDictionary)PropertyListParser.Parse(Path.Combine(d, "Info.plist"));
                txtResults.Text += $"Processing backup: {info["Device Name"]} version {info["Product Version"]}{Environment.NewLine}";
                return info["Unique Identifier"].ToString();
            }
            catch (Exception ex)
            {
                txtResults.Text += $"An Exception occurred while processing: {ex.Message}";
                return null;
            }
        }

        private async void btnGetSteamGuardData_Click(object sender, RoutedEventArgs e)
        {
            string iosBackups;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                iosBackups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apple Computer", "MobileSync", "Backup");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                iosBackups = string.Join(Path.DirectorySeparatorChar, new[] { "", "Users", Environment.UserName, "Library", "Application Support", "MobileSync", "Backup" });
            }
            else
            {
                iosBackups = null;
            }

            //var fileDialog = new OpenFileDialog
            //{
            //    AllowMultiple = false,
            //    Title = "Please select Manifest file",
            //    Filters = new()
            //    {
            //        new() { Name = "Manifest.mbdb", Extensions = new() { "mbdb" }, },
            //        new() { Name = "Manifest.db", Extensions = new() { "db" }, },
            //    },
            //};

            //if (iosBackups != null)
            //{
            //    if (!Directory.Exists(iosBackups))
            //    {
            //        txtResults.Text += $"Warn: Backup path not found, path: {iosBackups}";
            //    }
            //    else
            //    {
            //        fileDialog.Directory = iosBackups;
            //    }
            //}

            //var selectFilePath = (await fileDialog.ShowAsync(this)).FirstOrDefault();
            //if (File.Exists(selectFilePath))
            //{

            //}

            var folderDialog = new OpenFolderDialog();
            if (iosBackups != null)
            {
                if (!Directory.Exists(iosBackups))
                {
                    txtResults.Text += $"{Environment.NewLine}Warn: Backup path not found, path: {iosBackups}{Environment.NewLine}";
                }
                else
                {
                    folderDialog.Directory = iosBackups;
                }
            }

            var selectDirPath = await folderDialog.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(selectDirPath))
            {
                return;
            }
            if (!Directory.Exists(selectDirPath))
            {
                txtResults.Text += $"{Environment.NewLine}No ios backups found{Environment.NewLine}";
                return;
            }

            foreach (var d in Directory.GetDirectories(selectDirPath))
            {
                var name = new DirectoryInfo(d).Name;
                if (File.Exists(Path.Combine(d, "Manifest.mbdb")))
                    ProcessIOS9Backup(d);
                else if (File.Exists(Path.Combine(d, "Manifest.db")))
                    ProcessIOS10Backup(d);
                else
                {
                    txtResults.Text += $"Directory {name} is not in a recognized backup format.{Environment.NewLine}Listing contents of this directory.  Please open an issue and paste this listing as well as the Version of ios and itunes you are using.{Environment.NewLine}{Environment.NewLine}";
                    var count = 0;
                    foreach (var f in Directory.GetFiles(d))
                    {
                        var fileName = Path.GetFileName(f);
                        if (fileName == null) continue;

                        var filename = fileName.ToLower();

                        if (filename.Length == 40)
                        {
                            var chars = new[]
                            {"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f"};
                            filename = chars.Aggregate(filename, (current, c) => current.Replace(c, ""));
                            if (filename.Length == 0)
                            {
                                count++;
                                continue;
                            }
                        }
                        filename = fileName;
                        txtResults.Text += $"{filename}{Environment.NewLine}";
                    }
                    txtResults.Text += $"{Environment.NewLine}Done listing files - Skipped {count} files{Environment.NewLine}{Environment.NewLine}";
                    continue;
                }

                txtResults.Text += $"Done{Environment.NewLine}{Environment.NewLine}";
            }
        }
    }
}