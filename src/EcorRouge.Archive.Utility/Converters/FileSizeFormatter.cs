using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EcorRouge.Archive.Utility.Converters
{
    public class FileSizeFormatter : IValueConverter
    {
        private static string[] POSTFIXES = new string[] {
            "b", "Kb", "Mb", "Gb", "Tb"
        };

        public static string Format(double size)
        {
            int postfix = 0;
            while (size > 1024 && postfix < POSTFIXES.Length - 1)
            {
                size = size / 1024;
                postfix++;
            }

            return $"{size:0.###} {POSTFIXES[postfix]}";
        }


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            double size = System.Convert.ToDouble(value);

            return Format(size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
