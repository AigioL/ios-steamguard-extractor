using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Claunia.PropertyList;
using ios_steamguard_extractor;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace iOSSteamGuardExtractor;

public partial class MainWindow : Window
{
    readonly Button btnGetSteamGuardData;
    readonly (TextBox textBox, StringBuilder builder) txtResults;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        btnGetSteamGuardData = this.FindControl<Button>(nameof(btnGetSteamGuardData));
        var txtResultsControls = this.FindControl<TextBox>(nameof(txtResults));
        txtResultsControls.Text = $"CLR Version: {Environment.Version}{Environment.NewLine}Process Architecture: {RuntimeInformation.ProcessArchitecture}{Environment.NewLine}OS Architecture: {RuntimeInformation.OSArchitecture}{Environment.NewLine}{(Program.IsTest ? $"Test Json: {JsonConvert.SerializeObject(new SteamAuthenticator(), Formatting.Indented)}{Environment.NewLine}" : null)}";
        txtResults = (txtResultsControls, new(txtResultsControls.Text));
        btnGetSteamGuardData.Click += BtnGetSteamGuardData_Click;
    }

    void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    static int SearchBytes(IReadOnlyList<byte> haystack, IReadOnlyList<byte> needle, int start)
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

    bool ProcessSteamGuardFile(string fileName, string filePath, string deviceID)
    {
        StringBuilder b = txtResults.builder;
        b.AppendFormat("Processing {0}", fileName);
        b.AppendLine();
        try
        {
            var sgdata = BinaryPropertyListParser.Parse(File.ReadAllBytes(filePath));
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

            b.AppendLine("Alternatively, you can paste the above json text into {botname}.maFile in your ASF config directory, if you use ASF");
            b.AppendLine();
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
        return true;
    }

    void ProcessIOS9Backup(string dirPath)
    {
        var guid = ProcessInfoPlist(dirPath);
        if (guid == null) return;
        var data = File.ReadAllBytes(Path.Combine(dirPath, "Manifest.mbdb"));
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
            var hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes("AppDomain-com.valvesoftware.Steam-" + steamfilename));
            var hashstr = BitConverter.ToString(hash).Replace("-", "");
            if (File.Exists(Path.Combine(dirPath, hashstr)))
            {
                if (!ProcessSteamGuardFile(steamfilename, Path.Combine(dirPath, hashstr), guid)) break;
            }
            else
            {
                txtResults.builder.AppendLine($"Error: {steamfilename} is missing from ios backup, aborting");
                break;
            }
        }
    }

    void ProcessIOS10Backup(string dirPath)
    {
        try
        {
            var guid = ProcessInfoPlist(dirPath);
            if (guid == null) return;
            var dbConnection = new SqliteConnection($"Data Source=\"{Path.Combine(dirPath, "Manifest.db")}\";");
            dbConnection.Open();
            var query = "Select * from Files where domain is 'AppDomain-com.valvesoftware.Steam' and relativePath like 'Documents/Steamguard-%'";
            var dbCommand = new SqliteCommand(query, dbConnection);
            var dbReader = dbCommand.ExecuteReader();
            while (dbReader.Read())
            {
                var fileID = dbReader["fileID"].ToString();
                var startID = fileID.Substring(0, 2);
                var result = ProcessSteamGuardFile(dbReader["relativePath"].ToString(),
                    Path.Combine(dirPath, startID, fileID), guid);
                if (!result) break;
            }
            dbConnection.Close();
        }
        catch (SqliteException)
        {
            txtResults.builder.AppendLine("Error: Encrypted backups are not supported. You need to create a decrypted backup to proceed.");
        }
        catch (Exception ex)
        {
            txtResults.builder.AppendLine($"An Exception occurred while processing: {ex.Message}");
        }
    }

    string ProcessInfoPlist(string dirPath)
    {
        try
        {
            var info = (NSDictionary)PropertyListParser.Parse(Path.Combine(dirPath, "Info.plist"));
            txtResults.builder.AppendLine($"Processing backup: {info["Device Name"]} version {info["Product Version"]}");
            return info["Unique Identifier"].ToString();
        }
        catch (Exception ex)
        {
            txtResults.builder.AppendLine($"An Exception occurred while processing: {ex.Message}");
            return null;
        }
    }

    async void BtnGetSteamGuardData_Click(object sender, RoutedEventArgs e)
    {
        string iosBackups;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            iosBackups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apple Computer", "MobileSync", "Backup");
            if (!Directory.Exists(iosBackups))
            {
                iosBackups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Apple", "MobileSync", "Backup");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            iosBackups = string.Join(Path.DirectorySeparatorChar
#if NETFRAMEWORK || NETSTANDARD
                .ToString()
#endif
                , new[] { "", "Users", Environment.UserName, "Library", "Application Support", "MobileSync", "Backup" });
        }
        else
        {
            iosBackups = null;
        }

        var folderDialog = new OpenFolderDialog();
        if (iosBackups != null && Directory.Exists(iosBackups))
        {
            folderDialog.Directory = iosBackups;
        }

        var selectDirPath = await folderDialog.ShowAsync(this);
        if (string.IsNullOrWhiteSpace(selectDirPath))
        {
            return;
        }
        if (!Directory.Exists(selectDirPath))
        {
            txtResults.builder.AppendLine("No ios backups found");
            return;
        }

        var dirPaths = Directory.GetDirectories(selectDirPath);
        if (dirPaths.Any())
        {
            foreach (var dirPath in dirPaths)
            {
                var name = new DirectoryInfo(dirPath).Name;
                if (File.Exists(Path.Combine(dirPath, "Manifest.mbdb")))
                    ProcessIOS9Backup(dirPath);
                else if (File.Exists(Path.Combine(dirPath, "Manifest.db")))
                    ProcessIOS10Backup(dirPath);
                else
                {
                    txtResults.builder.AppendLine($"Directory {name} is not in a recognized backup format.{Environment.NewLine}Listing contents of this directory.  Please open an issue and paste this listing as well as the Version of ios and itunes you are using.");
                    txtResults.builder.AppendLine();
                    var count = 0;
                    foreach (var f in Directory.GetFiles(dirPath))
                    {
                        var fileName = Path.GetFileName(f);
                        if (fileName == null) continue;

                        var filename = fileName.ToLowerInvariant();

                        if (filename.Length == 40)
                        {
                            var chars = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
                            filename = chars.Aggregate(filename, (current, c) => current.Replace(c, ""));
                            if (filename.Length == 0)
                            {
                                count++;
                                continue;
                            }
                        }
                        filename = fileName;
                        txtResults.builder.AppendLine(filename);
                    }
                    txtResults.builder.AppendLine();
                    txtResults.builder.AppendLine($"Done listing files - Skipped {count} files");
                    txtResults.builder.AppendLine();
                    continue;
                }
                txtResults.builder.AppendLine("Done");
                txtResults.builder.AppendLine();
                txtResults.builder.AppendLine();
            }
        }
        else
        {
            txtResults.builder.AppendLine("No ios backups found");
        }

        txtResults.Flush();
        txtResults.textBox.ScrollToEnd();
    }
}