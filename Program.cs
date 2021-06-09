using IdentityModel.Client;
using iSAMS.Utilities.Reporting.CustomFieldRenaming.Exceptions;
using iSAMS.Utilities.Reporting.CustomFieldRenaming.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace iSAMS.Utilities.Reporting.CustomFieldRenaming
{
    class Program
    {
        private static string AccessToken = null;
        private static HttpClient ApiClient = null;
        private static readonly string ApiEndpoint = "/api/students/{schoolId}/customFields";
        public static ScriptConfiguration Config = null;
        private static int CustomFieldId = -1;
        private static string CustomFieldResultDirectory_Failed = null;
        private static string CustomFieldResultDirectory_Success = null;
        private static List<string> FileNames = null;
        public const string RestApiScope = "restapi";
        private static SummaryStore Summary = new SummaryStore();
        private const string TokenPath = "auth/connect/token";

        static void Main(string[] args)
        {
            try
            {
                GetConfig();
                ValidateConfig();

                EnsureDirectoryExists(Config.TargetDirectory);
                GetFiles();
                FilterFilesToProcessable();

                EnsureFileNamesHasLength();

                AuthenticateApi().GetAwaiter().GetResult();

                SetResultDirectories();
                ProcessFiles();

                SaveSummary();
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
            finally
            {
                EndProgram();
            }
        }


        #region Config Methods

        private static void GetConfig()
        {
            Console.WriteLine("Getting utility settings...");
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            try
            {
                if (File.Exists(configPath))
                {
                    Console.WriteLine("Utility settings found.");
                    LoadConfigJson(configPath);
                }
                else
                {
                    LogError($"No configuration file found. Please ensure 'config.json' exists within {Directory.GetCurrentDirectory()}");
                    EndProgram();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }

        private static void LoadConfigJson(string path)
        {
            try
            {
                Console.WriteLine("Parsing utility settings...");
                using (StreamReader reader = new StreamReader(path))
                {
                    string json = reader.ReadToEnd();
                    Config = JsonConvert.DeserializeObject<ScriptConfiguration>(json);
                    Console.WriteLine("Utility settings parsed successfully.");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void ValidateConfig()
        {
            Console.WriteLine("Verifying utility settings...");
            var errors = new List<string>();
            if (string.IsNullOrEmpty(Config.Domain))
                errors.Add("No 'Domain' value was found within the configuration file.");

            if (string.IsNullOrEmpty(Config.RestApiClientId))
                errors.Add("No 'RestApiClientId' value was found within the configuration file.");

            if (string.IsNullOrEmpty(Config.RestApiClientSecret))
                errors.Add("No 'RestApiClientSecret' value was found within the configuration file.");

            if (string.IsNullOrEmpty(Config.TargetDirectory))
                errors.Add("No 'TargetDirectory' value was found within the configuration file.");

            if (string.IsNullOrEmpty(Config.CustomFieldName))
                errors.Add("No 'CustomFieldName' value was found within the configuration file.");

            if (errors.Count > 0)
            {
                LogError("Configuration invalid. Please address the following issues before continuing.");
                LogError(string.Join(Environment.NewLine, errors));
                EndProgram();
            }

            Console.WriteLine("Utility settings verified.");
        }

        #endregion


        #region File/Directory Methods

        private static void CopyFileToResultFolder(bool success, string currentFilePath, string newFileName, string message = null)
        {
            if (!success)
            {
                var newPath = Path.Combine(CustomFieldResultDirectory_Failed, Path.GetFileName(currentFilePath));
                if (!Directory.Exists(CustomFieldResultDirectory_Failed))
                {
                    Directory.CreateDirectory(CustomFieldResultDirectory_Failed);
                }

                if(Path.GetFullPath(currentFilePath) != Path.GetFullPath(newPath))
                {
                    File.Copy(currentFilePath, newPath, true);
                }

                Summary.Add($"{Path.GetFileName(currentFilePath)} failed to be renamed. [{message}]", false);
            }
            else
            {
                var newPath = Path.Combine(CustomFieldResultDirectory_Success, $"{newFileName}.pdf");
                if (!Directory.Exists(CustomFieldResultDirectory_Success))
                    Directory.CreateDirectory(CustomFieldResultDirectory_Success);

                File.Copy(currentFilePath, newPath, true);
                Summary.Add($"{Path.GetFileName(currentFilePath)} => {Path.GetFileName(newPath)}", true);
            }
        }

        private static bool DoesDirectoryContainSubDirectory(string directory, string subDirectory)
        {
            string cleanedDirectory = Regex.Replace(directory, @"\/|\\", "/").Trim('/').ToLower();
            string cleanedSubDirectory = Regex.Replace(subDirectory, @"\/|\\", "/").Trim('/').ToLower();

            return cleanedDirectory.EndsWith(cleanedSubDirectory);
        }

        private static void EnsureDirectoryExists(string path)
        {
            Console.WriteLine("Validating directory...");
            var exists = Directory.Exists(path);
            if (!exists)
            {
                LogError($"The given directory {path} could not be found.");
                EndProgram();
            }
            Console.WriteLine("Directory found.");
        }

        private static void EnsureFileNamesHasLength()
        {
            if (FileNames.Count == 0)
            {
                LogError("None of the discovered files are able to be processed. Please ensure the file names follow the '[SCHOOLID].pdf' format.");
                EndProgram();
            }

            Console.WriteLine($"{FileNames.Count} files found.");
        }

        private static bool FileShouldBeProcessed(string fileName)
        {
            return Regex.Match(Path.GetFileName(fileName), "^[0-9]+.pdf").Success;
        }

        private static void FilterFilesToProcessable()
        {
            FileNames = FileNames.Where(n => FileShouldBeProcessed(n)).ToList();
        }

        private static void GetFiles()
        {
            Console.WriteLine("Getting directory contents...");
            FileNames = Directory.GetFiles(Config.TargetDirectory).ToList();
        }

        private static void ProcessFile(string fileName)
        {
            GetNewFileName(fileName, Path.GetFileNameWithoutExtension(fileName)).GetAwaiter().GetResult();
        }

        private static void ProcessFiles()
        {
            Console.WriteLine("Processing files...");
            CreateApiClient();

            for (int i = 0; i < FileNames.Count; i++)
            {
                RenderProgress(i + 1);
                var currentFile = FileNames[i];
                try
                {
                    ProcessFile(currentFile);
                }
                catch (Exception ex)
                {
                    var error = $"Something went wrong while processing the file {currentFile}. [{ex.Message}]";
                    LogError(error);
                    CopyFileToResultFolder(false, currentFile, null, error);
                }
            }

            DisposeApiClient();
            Console.WriteLine();
            Console.WriteLine("File processing complete.");
        }

        private static void SetResultDirectories()
        {
            var failedName = "Failed";
            var successName = "Success";
            var failedSubDirectory = Path.Combine(Config.CustomFieldName, failedName);
            var successSubDirectory = Path.Combine(Config.CustomFieldName, successName);

            //Check whether user is re-running existing failed jobs
            if (DoesDirectoryContainSubDirectory(Config.TargetDirectory, failedSubDirectory))
            {
                CustomFieldResultDirectory_Failed = Config.TargetDirectory;
                CustomFieldResultDirectory_Success = Config.TargetDirectory.TrimEnd(failedName.ToCharArray()) + successName;
            }
            else
            {
                CustomFieldResultDirectory_Failed = Path.Combine(Config.TargetDirectory, failedSubDirectory);
                CustomFieldResultDirectory_Success = Path.Combine(Config.TargetDirectory, successSubDirectory);
            }
        }

        #endregion


        #region API Methods

        private static async Task AuthenticateApi()
        {
            TokenResponse tokenResponse;
            using (var httpClient = new HttpClient())
            {
                Console.WriteLine("Retrieving the discovery document...");
                var discoveryDocumentResponse = await httpClient.GetDiscoveryDocumentAsync(Config.Authority);
                if (discoveryDocumentResponse.IsError)
                {
                    throw new AccessTokenException(
                        $"[{discoveryDocumentResponse.HttpStatusCode}] Error retrieving the discovery document [{discoveryDocumentResponse.Error}].");
                }

                Console.WriteLine("Retrieved the discovery document.");

                var authTokenUrl = $"{Config.Domain.TrimEnd('/')}/{TokenPath.TrimStart('/')}";
                var apiClientCredentials = new ClientCredentialsTokenRequest();
                apiClientCredentials.Address = authTokenUrl;
                apiClientCredentials.ClientId = Config.RestApiClientId;
                apiClientCredentials.ClientSecret = Config.RestApiClientSecret;
                apiClientCredentials.Scope = RestApiScope;

                Console.WriteLine($"Authenticating {Config.RestApiClientId}...");
                tokenResponse = await httpClient.RequestClientCredentialsTokenAsync(apiClientCredentials);
                if (tokenResponse.IsError)
                {
                    throw new AccessTokenException(
                        $"[{tokenResponse.HttpStatusCode}] Error authenticating [{tokenResponse.Error}].");
                }

                AccessToken = tokenResponse.AccessToken;
                Console.WriteLine("Authenticated successfully.");
            }
        }

        private static void CreateApiClient()
        {
            var apiClient = new HttpClient();

            apiClient.BaseAddress = new Uri($"{Config.Domain.TrimEnd('/')}/api");
            apiClient.DefaultRequestHeaders.Clear();

            apiClient.SetBearerToken(AccessToken);
            SetRequestHeaders(apiClient, "application/hal+json");
            ApiClient = apiClient;
        }

        private static void DisposeApiClient()
        {
            ApiClient.Dispose();
        }

        private static async Task GetNewFileName(string currentFileName, string schoolId)
        {
            var apiPath = $"{Config.Domain.TrimEnd('/')}{ApiEndpoint}";
            apiPath = apiPath.Replace("{schoolId}", schoolId);

            if (CustomFieldId > -1)
            {
                apiPath = $"{apiPath}/{CustomFieldId}";
            }

            var response = await InternalGetAsync(ApiClient, apiPath);

            if (!response.IsSuccessStatusCode)
            {
                throw new RestApiException(apiPath,
                    $"[{response.StatusCode}] Error retrieving the Custom Field value [{response.ReasonPhrase}].");
            }

            var body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(body))
            {
                throw new RestApiException(apiPath,
                    "Request succeeded but Custom Field is empty.");
            }

            var customFieldValues = JsonConvert.DeserializeObject<CustomFieldsCollection>(body).CustomFields;
            CustomFieldValue customFieldValue;
            if (CustomFieldId > -1)
            {
                customFieldValue = customFieldValues.FirstOrDefault();
            }
            else
            {
                customFieldValue = customFieldValues.FirstOrDefault(x => string.Equals(x.Name, Config.CustomFieldName, StringComparison.InvariantCultureIgnoreCase));

                if (customFieldValue == null)
                {
                    throw new RestApiException(apiPath,
                        $"Request succeeded but Custom Field '{Config.CustomFieldName}' could not be found.");
                }

                CustomFieldId = customFieldValue.Id;
            }

            if (string.IsNullOrEmpty(customFieldValue?.Value))
            {
                throw new RestApiException(apiPath,
                    "Request succeeded but Custom Field is empty.");
            }

            CopyFileToResultFolder(true, currentFileName, customFieldValue.Value);
        }

        private static Task<HttpResponseMessage> InternalGetAsync(HttpClient apiClient, string apiPath)
        {
            return apiClient.GetAsync(apiPath);
        }

        private static void SetRequestHeaders(HttpClient apiClient, string accept)
        {
            apiClient.DefaultRequestHeaders.Accept.Clear();
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        #endregion


        #region Console Methods

        private static void EndProgram()
        {
            Console.WriteLine("This utility can now be closed.");
            Console.ReadLine();
            Environment.Exit(0);
        }

        private static void LogError(string message)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void RenderProgress(int currentFileIndex)
        {
            var total = FileNames.Count;
            Console.CursorLeft = 0;
            Console.Write($"Processing {currentFileIndex} of {total}          "); //blanks at the end remove any excess
        }

        #endregion


        #region Summary Methods

        private static void SaveSummary()
        {
            var logPath = Path.Combine(Directory.GetParent(CustomFieldResultDirectory_Success).FullName, $"event_log_{DateTime.Now.Ticks}.txt");
            File.WriteAllLines(logPath, Summary.Log);

            Console.WriteLine($"{Summary.SuccessfulRequests} Successful.");
            Console.WriteLine($"{Summary.FailedRequests} Failed.");
            Console.WriteLine($"Event log exported to {logPath}");
        }

        #endregion

    }
}
