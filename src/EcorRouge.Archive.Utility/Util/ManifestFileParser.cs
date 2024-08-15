using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zip;
using log4net;

namespace EcorRouge.Archive.Utility.Util
{
    public class ManifestFileParser : IDisposable
    {
        private const char DEFAULT_PATH_SEPARATOR = '|';

        internal static readonly ILog log = LogManager.GetLogger(typeof(ManifestFileParser));

        private ManifestFile _inputFile;
        private Stream _currentZipStream;
        private StreamReader _reader;
        private ZipFile _zipFile;
        private ZipEntry[] _zipEntries;
        private int _currentZipEntry = 0;
        private char _pathSeparator = DEFAULT_PATH_SEPARATOR;

        //$"sep={CSV_SEP}"
        //"File Name", "File Size", "Created At (UTC)", "Zip File Name", "Generated File Name", "Original Path"
        //{fileName}{CSV_SEP}{fileSize}{CSV_SEP}{createdAt}{CSV_SEP}{zipFileName}{CSV_SEP}{newFileName}{CSV_SEP}{origPath}

        private static (int columnCount, char pathSeparator) DetectColumnCount(StreamReader reader, char defaultPathSeparator)
        {
            int columns = 0;
            var separator = defaultPathSeparator;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (String.IsNullOrEmpty(line))
                {
                    continue;
                }

                if(line.StartsWith("sep=",StringComparison.InvariantCultureIgnoreCase))
                {
                    separator = line.Split("=")[1].Trim()[0];
                    continue;
                }

                if (line.Contains(separator))
                {
                    var parts = line.Split(separator);

                    if (parts.Length == columns)
                    {
                        break;
                    }

                    columns = parts.Length;
                }
            }

            return (columns, separator);
        }

        public static ManifestFile ScanFile(string fileName, string keypair)
        {
            log.Debug($"Scanning file: {fileName}");

            var result = new ManifestFile()
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

                    using (var reader = new StreamReader(entry.OpenReader()))
                    {
                        (result.Columns, pathSeparator) = DetectColumnCount(reader, DEFAULT_PATH_SEPARATOR);
                        log.Debug($"Detected column count: {result.Columns}, separator {pathSeparator}");
                    }

                    if (result.Columns > 0)
                    {
                        break;
                    }
                }
            }
            else
            {
                using (var reader = new StreamReader(fileName))
                {
                    (result.Columns, pathSeparator) = DetectColumnCount(reader, DEFAULT_PATH_SEPARATOR);

                    log.Debug($"Detected column count: {result.Columns}, separator {pathSeparator}");
                }
            }

            if (result.Columns == 6)
            {
                using var parser = OpenFile(result, pathSeparator);

                ManifestFileEntry entry;
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

        public static ManifestFileParser OpenFile(ManifestFile inputFile, char? pathSeparator)
        {
            var result = new ManifestFileParser();

            result._pathSeparator = pathSeparator ?? DEFAULT_PATH_SEPARATOR;
            result._inputFile = inputFile;

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

        private static bool IsHeader(string line, char separator) => line?.StartsWith($"File Name{separator}File Size{separator}Created At", StringComparison.InvariantCultureIgnoreCase) ?? false;

        private ManifestFileEntry GetNextEntryInternal(StreamReader reader, int columns)
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("sep="))
                {
                    _pathSeparator = line.Split("=")[1].Trim()[0];
                    continue;
                }

                if (IsHeader(line, _pathSeparator))
                {
                    continue;
                }

                if (line.Split(_pathSeparator).Length != columns)
                {
                    log.Warn($"Invalid number of columns in line: {line}!");
                    continue;
                }

                return ParseLine(line);
            }

            return null;
        }

        private static string DecodeB64IfNeeded(string target)
        {
            return target.StartsWith("?b64?", StringComparison.InvariantCultureIgnoreCase)
                ? Encoding.UTF8.GetString(Convert.FromBase64String(target.Substring(5)))
                : target;
        }

        private static ManifestFileEntry ParseLine(string line)
        {
            var parts = line.Split(DEFAULT_PATH_SEPARATOR);

            //"File Name", "File Size", "Created At (UTC)", "Zip File Name", "Generated File Name", "Original Path"

            string fileName = parts[0].Trim();

            if (!int.TryParse(parts[1], out var fileSize))
            {
                log.Warn($"Unable to parse length: {parts[1]} of file {fileName}");
            }

            DateTime? createdAtUtc = null;
            string createdDateTimeStr = parts[2]; // an: note that according to the input, we currently receive Modified DateTime here rather than Created
            if (DateTime.TryParse(createdDateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                createdAtUtc = dt;
            }
            else if (!string.IsNullOrWhiteSpace(createdDateTimeStr))
            {
                log.Warn($"Unable to parse datetime: {createdDateTimeStr} of file {fileName}");
            }

            string zipFileName = parts[3].Trim();
            string generatedFileName = parts[4].Trim();
            string originalPath = DecodeB64IfNeeded(parts[5].Trim());

            return new ManifestFileEntry()
            {
                FileName = fileName,
                FileSize = fileSize,
                CreatedAtUtc = createdAtUtc,
                ZipFileName = zipFileName,
                GeneratedFileName = generatedFileName,
                OriginalPath = originalPath,
                RawEntryContent = line                
            };
        }

        public ManifestFileEntry GetNextEntry()
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
