using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zip;
using log4net;

namespace EcorRouge.Archive.Utility.Util
{
    public class InputFileParser : IDisposable
    {
        private const char DEFAULT_PATH_SEPARATOR = '|';

        internal static readonly ILog log = LogManager.GetLogger(typeof(InputFileParser));

        private InputFile _inputFile;
        private Stream _currentZipStream;
        private StreamReader _reader;
        private ZipFile _zipFile;
        private ZipEntry[] _zipEntries;
        private int _currentZipEntry = 0;
        private char _pathSeparator = DEFAULT_PATH_SEPARATOR;
        private string _connectorPrefix;
        private bool _isWebExportOutput;

        private static int DetectColumnCount(StreamReader reader, char pathSeparator)
        {
            int columns = 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (String.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line.Contains(pathSeparator))
                {
                    var parts = line.Split(pathSeparator);

                    if (parts.Length == columns)
                    {
                        break;
                    }

                    columns = parts.Length;
                }
            }

            return columns;
        }

        public static InputFile ScanFile(string fileName, string[] connectorsPrefixes)
        {
            log.Debug($"Scanning file: {fileName}");

            var result = new InputFile()
            {
                FileName = fileName,
                IsZip = false,
                Columns = 0,
                TotalFiles = 0,
                TotalFilesSize = 0
            };

            if (Path.GetExtension(fileName).Equals(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                result.IsZip = true;
            }

            char pathSeparator = DEFAULT_PATH_SEPARATOR;

            if (result.IsZip)
            {
                using var zip = ZipFile.Read(fileName);
                foreach (var entry in zip.Entries)
                {
                    log.Debug($"  zip entry={entry.FileName}");

                    if (entry.FileName.StartsWith("__")) // Skip __MACOSX
                        continue;

                    Func<StreamReader> createReader = () => new StreamReader(entry.OpenReader());
                    result.ConnectorMarker = DetectConnectorPrefix(createReader, connectorsPrefixes);
                    (result.Columns, pathSeparator) = DetectColumnCount(createReader);
                    log.Debug($"Detected column count: {result.Columns}, separator {pathSeparator}, connector marker {result.ConnectorMarker}");

                    if (result.Columns > 0)
                    {
                        break;
                    }
                }
            }
            else
            {
                Func<StreamReader> createReader = () => new StreamReader(fileName, Encoding.UTF8);
                result.ConnectorMarker = DetectConnectorPrefix(createReader, connectorsPrefixes);
                (result.Columns, pathSeparator) = DetectColumnCount(() => new StreamReader(fileName, Encoding.UTF8));
                log.Debug($"Detected column count: {result.Columns}, separator {pathSeparator}, connector marker {result.ConnectorMarker}");
            }

            if (result.Columns == 3 || result.Columns == 11 || result.ConnectorMarker != null)
            {
                using var parser = OpenFile(result, pathSeparator);

                InputFileEntry entry;
                while ((entry = parser.GetNextEntry()) != null)
                {
                    result.TotalFiles++;
                    result.TotalFilesSize += entry.FileSize;
                }
            }
            else
            {
                log.Warn($"Unsupported column count: {result.Columns}");
            }

            return result;
        }

        private static (int columnCount, char pathSeparator) DetectColumnCount(Func<StreamReader> createReader)
        {
            foreach (char separator in "|:;")
            {
                using var reader = createReader();
                int columnCount = DetectColumnCount(reader, separator);
                if (columnCount > 0)
                {
                    return (columnCount, separator);
                }
            }

            return (0, DEFAULT_PATH_SEPARATOR);
        }

        private static string DetectConnectorPrefix(Func<StreamReader> createReader, string[] possiblyUsedMarkers)
        {
            if (possiblyUsedMarkers?.Length == 0) return null;

            foreach (string marker in possiblyUsedMarkers)
            {
                string detectedMarker = marker;

                using var reader = createReader();

                string line = null;

                int lineCount = 0;
                do
                {
                    line = reader.ReadLine();
                    lineCount++;

                    if (IsWebExportHeader(line)) continue;

                    if (!line?.Contains(marker) ?? false)
                    {
                        detectedMarker = null;
                        break;
                    }

                } while (line != null && lineCount < 100);

                if (detectedMarker != null)
                {
                    return detectedMarker;
                }
            }

            return null;
        }

        public static InputFileParser OpenFile(InputFile inputFile, char? pathSeparator)
        {
            var result = new InputFileParser();

            result._pathSeparator = pathSeparator ?? DEFAULT_PATH_SEPARATOR;
            result._inputFile = inputFile;
            result._connectorPrefix = inputFile.ConnectorMarker;

            if (inputFile.IsZip)
            {
                result._zipFile = ZipFile.Read(inputFile.FileName);
                result._currentZipEntry = 0;

                var entries = new List<ZipEntry>();
                foreach (var zipEntry in result._zipFile.Entries)
                {
                    log.Debug($"  zip entry={zipEntry.FileName}");

                    if (zipEntry.FileName.StartsWith("__")) // Skip __MACOSX
                        continue;

                    entries.Add(zipEntry);
                }

                result._zipEntries = entries.ToArray();
            }
            else
            {
                result._reader = new StreamReader(inputFile.FileName, Encoding.UTF8);
            }

            return result;
        }

        private static bool IsWebExportHeader(string line) => line?.StartsWith("Collector|Drive|Name", StringComparison.InvariantCultureIgnoreCase) ?? false;

        private InputFileEntry GetNextEntryInternal(StreamReader reader, int columns)
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (!_isWebExportOutput && IsWebExportHeader(line))
                {
                    // mark as web export and skip header line
                    _isWebExportOutput = true;
                    _pathSeparator = DEFAULT_PATH_SEPARATOR;
                    continue;
                }

                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Split(_pathSeparator).Length != columns)
                {
                    log.Warn($"Invalid number of columns in line: {line}!");
                    continue;
                }

                if (_connectorPrefix != null)
                {
                    int indexOfPrefix = line.IndexOf(_connectorPrefix);

                    if (indexOfPrefix < 0)
                    {
                        log.Warn($"Skipping line since it does not contain connector prefix ({_connectorPrefix}). {Environment.NewLine}{line}");
                        continue;
                    }

                    return _isWebExportOutput
                        ? ParseLineInWebExportFormat(line)
                        : ParseLineInCloudConnectorFormat(line);
                }
                else
                {
                    return ParseLineInWebExportFormat(line);
                }
            }

            return null;
        }


        InputFileEntry ParseLineInWebExportFormat(string line)
        {
            // e.g. AZReveles|sharepoint|Bear Branch59.jpg|?b64?YiE4ZWNUUDRKcmQwcWlhYzJnY1ZwLTZMb1RJZmFqcnFaUGlJN1ZsLXNNZGlrdzRhblFNRFlxUnFmY1RUV3U4dVdPOjAxM0ZQVFpaT09SRlZES0hFUlJWRkxDUFVJWUlNTFlOR0M=|0|04/14/2013|Risk Management|jpg|Media|> 10| < 10KB
            var parts = line.Split(DEFAULT_PATH_SEPARATOR);

            // some old format? leaving as is just in case
            if (parts.Length == 3)
            {
                if (!Int64.TryParse(parts[1], out var length))
                {
                    log.Warn($"Unable to parse length: {parts[1]} of file {parts[0]}");
                }

                return new InputFileEntry()
                {
                    FileName = Path.GetFileName(parts[0]),
                    FileSize = length,
                    Path = parts[0]
                };
            }

            // Searching like this is safer - in case filename contains '|' we will still correctly detect the cloudpath, and we will get the part of the filename with the extension
            // It would be better to encode the name and the cloudpath together, but Jay doesn't want to for now.
            string encodedComponent = parts.FirstOrDefault(c => c.StartsWith("?b64?"));

            string cloudPath;
            int indexOfCloudPath;
            if (encodedComponent != null)
            {
                indexOfCloudPath = Array.IndexOf(parts, encodedComponent);
                cloudPath = DecodeB64IfNeeded(encodedComponent);
            }
            else
            {
                indexOfCloudPath = 3;
                cloudPath = parts[indexOfCloudPath];
            }

            string filename = parts[indexOfCloudPath - 1];

            if (!int.TryParse(parts[indexOfCloudPath+1], out var fileSize))
            {
                log.Warn($"Unable to parse length: {parts[indexOfCloudPath + 1]} of file {filename}");
            }

            DateTime? createdAtUtc = null;
            string createdDateTimeStr = parts[indexOfCloudPath + 2]; // an: note that according to the input, we currently receive Modified DateTime here rather than Created
            if (DateTime.TryParse(createdDateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                createdAtUtc = dt;
            }
            else if (!string.IsNullOrWhiteSpace(createdDateTimeStr))
            {
                log.Warn($"Unable to parse datetime: {createdDateTimeStr} of file {filename}");
            }

            return new InputFileEntry()
            {
                Path = cloudPath,
                FileName = filename,
                FileSize = fileSize,
                RawEntryContent = line,
                CreatedAtUtc = createdAtUtc
            };
        }


        InputFileEntry ParseLineInCloudConnectorFormat(string line)
        {
            // parsing logic ported from the cloud collector
            // sample 1: msemail
            // admin@6hhbft.onmicrosoft.com/Inbox/Microsoft Entra ID Protection Weekly Digest|0|1706633164000|MsMail2;3f56ff8b-02c5-47ea-9b03-9d8967a895db;AAMkADNkMzFhNjJlLTQxMzYtNGQ3ZS1hYTIwLWZkN2YyODBhZTAxNgBGAAAAAACy8MYsl0qvT7oucLw2vz41BwDwug8XjsiETIUKfe9OKFBTAAAAAAEMAADwug8XjsiETIUKfe9OKFBTAAAfrTweAAA=;MESSAGE_BODY:1706632844000:0:admin@6hhbft.onmicrosoft.comInboxMicrosoft Entra ID Protection Weekly Digest

            // sample 2: onedrive
            // ?b64?YiE4ZWNUUDRKcmQwcWlhYzJnY1ZwLTZMb1RJZmFqcnFaUGlJN1ZsLXNNZGlrdzRhblFNRFlxUnFmY1RUV3U4dVdPOjAxM0ZQVFpaT09SRlZES0hFUlJWRkxDUFVJWUlNTFlOR0M=:1705483604000:40192:OneDrive2HenriettaM@6hhbft.onmicrosoft.com013FPTZZIOMYPL4KDYYVAIS4X2KMVIDBM7sample_file_00f4f2e1-2f73-4733-a26e-ca555c632a2f.txt

            // sample 3: gmail
            // ?b64?R29vZ2xlIFdvcmtzcGFjZTogWW91ciBpbnZvaWNlIGlzIGF2YWlsYWJsZSBmb3Igd29ya3NwYWNldGVzdC5vbmxpbmUudHh0|57996|1669928724000|?b64?YWRtaW5Ad29ya3NwYWNldGVzdC5vbmxpbmU6MTg0Y2Y4MjJkMjg2ZjliZjpNZXNzYWdlQm9keQ==:1669928724000:57996:Google Workspace Your invoice is available for workspacetest.online.txt

            string lastComponent = line;
            string cloudPath = null;
            string displayFilePath = null;
            long fileSize = 0;
            DateTime? modifiedAtUtc = null;
            DateTime? createdAtUtc = null;

            if (line.Contains("|"))
            {
                var components = line.Split("|");
                displayFilePath = components[0];
                lastComponent = components[^1];

                if (long.TryParse(components[2], out var modifiedOnUnixMs))
                {
                    modifiedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(modifiedOnUnixMs).UtcDateTime;
                }
            }

            if (lastComponent.Contains(":"))
            {
                var parts = lastComponent.Split(':');
                cloudPath = parts[0];

                if (parts.Length > 2)
                {
                    Int64.TryParse(parts[^2], out fileSize);
                }

                if (parts.Length > 3 && long.TryParse(parts[^3], out var createdOnUnixMs))
                {
                    createdAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(createdOnUnixMs).UtcDateTime;
                }
            }

            return new InputFileEntry()
            {
                Path = DecodeB64IfNeeded(cloudPath ?? lastComponent),
                FileName = DecodeB64IfNeeded(displayFilePath ?? cloudPath),
                FileSize = fileSize,
                CreatedAtUtc = createdAtUtc ?? modifiedAtUtc,
                RawEntryContent = line
            };
        }
        string DecodeB64IfNeeded(string target)
        {
            return target.StartsWith("?b64?", StringComparison.InvariantCultureIgnoreCase)
                ? Encoding.UTF8.GetString(Convert.FromBase64String(target.Substring(5)))
                : target;
        }

        public InputFileEntry GetNextEntry()
        {
            if (_inputFile.IsZip)
            {
                while (_currentZipEntry < _zipEntries.Length)
                {
                    if (_reader == null)
                    {
                        if (_currentZipEntry < _zipEntries.Length)
                        {
                            _currentZipStream = _zipEntries[_currentZipEntry].OpenReader();
                            _reader = new StreamReader(_currentZipStream);

                            _isWebExportOutput = false;
                        }
                        else
                        {
                            return null;
                        }
                    }

                    var entry = GetNextEntryInternal(_reader, _inputFile.Columns);

                    if (entry == null)
                    {
                        _currentZipStream?.Dispose();
                        _currentZipStream = null;

                        _reader?.Dispose();
                        _reader = null;
                    }
                    else
                    {
                        return entry;
                    }

                    _currentZipEntry++;
                }

                return null;
            }
            else
            {
                return GetNextEntryInternal(_reader, _inputFile.Columns);
            }

        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }

            if (_currentZipStream != null)
            {
                _currentZipStream.Dispose();
                _currentZipStream = null;
            }

            if (_zipFile != null)
            {
                _zipFile.Dispose();
                _zipFile = null;
            }
        }
    }
}
