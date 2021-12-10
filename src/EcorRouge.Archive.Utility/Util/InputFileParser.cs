using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EcorRouge.Archive.Utility.ViewModels;
using Ionic.Zip;
using log4net;

namespace EcorRouge.Archive.Utility.Util
{
    public class InputFileParser : IDisposable
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(InputFileParser));

        private InputFile _inputFile;
        private Stream _currentZipStream;
        private StreamReader _reader;
        private ZipFile _zipFile;
        private ZipEntry[] _zipEntries;
        private int _currentZipEntry = 0;

        private static int DetectColumnCount(StreamReader reader)
        {
            int columns = 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (String.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line.Contains("|"))
                {
                    var parts = line.Split("|");

                    if (parts.Length == columns)
                    {
                        break;
                    }

                    columns = parts.Length;
                }
            }

            return columns;
        }

        public static InputFile ScanFile(string fileName)
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

            if (result.IsZip)
            {
                using (var zip = ZipFile.Read(fileName))
                {
                    foreach (var entry in zip.Entries)
                    {
                        log.Debug($"  zip entry={entry.FileName}");

                        if (entry.FileName.StartsWith("__")) // Skip __MACOSX
                            continue;

                        using (var stream = entry.OpenReader())
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                result.Columns = DetectColumnCount(reader);

                                log.Debug($"Detected column count: {result.Columns}");
                            }
                        }

                        if (result.Columns > 0)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                using (var reader = new StreamReader(fileName, Encoding.UTF8))
                {
                    result.Columns = DetectColumnCount(reader);

                    log.Debug($"Detected column count: {result.Columns}");
                }
            }

            if (result.Columns == 3 || result.Columns == 11)
            {
                using (var parser = OpenFile(result))
                {
                    InputFileEntry entry;
                    while ((entry = parser.GetNextEntry()) != null)
                    {
                        result.TotalFiles++;
                        result.TotalFilesSize += entry.FileSize;
                    }
                }
            }
            else
            {
                log.Warn($"Unsupported column count: {result.Columns}");
            }

            return result;
        }

        public static InputFileParser OpenFile(InputFile inputFile)
        {
            var result = new InputFileParser();

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

        private static InputFileEntry GetNextEntryInternal(StreamReader reader, int columns)
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("Collector|Drive|Name", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var parts = line.Split("|");
                if (parts.Length != columns)
                {
                    log.Warn($"Invalid number of columns in line: {line}!");
                    continue;
                }

                if (columns == 3)
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
                } else if (columns == 11)
                {
                    if (!Int64.TryParse(parts[4], out var length))
                    {
                        log.Warn($"Unable to parse length: {parts[4]} of file {parts[2]}");
                    }

                    return new InputFileEntry()
                    {
                        FileName = parts[2],
                        FileSize = length,
                        Path = parts[3]
                    };
                }
                else
                {
                    log.Warn($"Unsupported number of columns: {columns}!");
                    return null;
                }
            }

            return null;
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
