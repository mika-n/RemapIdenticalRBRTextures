using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RemapIdenticalRBRTextures
{
    //
    // INI file helper class
    //
    public class INIFile
    {
        private readonly string _INIFileName;       // Full path filename
        private readonly string _INIFileNamePart;   // Filename part only

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public enum BoolValueType { TrueFalse, OneZero, YesNo }

        private readonly bool _FileExists;
        public bool FileExists { get => _FileExists; }

        public INIFile()
        {
            // nothing to see here
        }

        public INIFile(string iniFileName)
        {
            this._INIFileName = iniFileName;
            this._INIFileNamePart = Path.GetFileNameWithoutExtension(iniFileName);
            this._FileExists = File.Exists(iniFileName);
        }

        public void WriteValue(string Section, string Key, string Value)
        {
            if (string.Compare(this.ReadValue(Section, Key, ""), Value, true) != 0)
                WritePrivateProfileString(Section, Key, Value, this._INIFileName);
        }

        public void WriteValueInt(string Section, string Key, int Value)
        {
            this.WriteValue(Section, Key, Value.ToString());
        }

        public void WriteValueFloat(string Section, string Key, float Value)
        {
            CultureInfo cultureUS = new CultureInfo("en-US");
            if (cultureUS.NumberFormat.NumberDecimalSeparator != ".")
                cultureUS.NumberFormat.NumberDecimalSeparator = ".";

            this.WriteValue(Section, Key, string.Format(cultureUS, "{0:0.######}", Value));
        }

        public void WriteValueBool(string Section, string Key, bool Value, BoolValueType boolValueType = BoolValueType.TrueFalse)
        {
            string strValue;
            if (boolValueType == BoolValueType.OneZero)
                strValue = (Value ? 1 : 0).ToString();
            else if (boolValueType == BoolValueType.YesNo)
                strValue = (Value ? "yes" : "no");
            else
                strValue = (Value ? "true" : "false");

            this.WriteValue(Section, Key, strValue);
        }

        public string ReadValue(string Section, string Key, string defaultValue = "")
        {
            if (!this.FileExists)
                return defaultValue;

            StringBuilder strBuffer = new StringBuilder(256);
            int i = GetPrivateProfileString(Section, Key, defaultValue, strBuffer, 256 - 2, this._INIFileName);

            // Trim and remove enclosing "xxx" double quotes and line end comments
            string resultText = strBuffer.ToString().Trim();

            int commentPos = resultText.IndexOf(';');
            if (commentPos >= 0)
            {
                if (commentPos == 0)
                    resultText = string.Empty;
                else
                    resultText = (resultText[0..(commentPos - 1)]).Trim();
            }

            if (resultText.Length >= 2)
            {
                if (resultText.Length >= 3 && resultText[0] == '"' && resultText[^1] == '"')
                    resultText = resultText[1..^1];
                else
                {
                    if (resultText[resultText.Length - 1] == '"')
                        resultText = resultText.Remove(resultText.Length - 1, 1);

                    if (resultText[0] == '"')
                    {
                        if (resultText.Length >= 2)
                            resultText = resultText[1..];
                        else
                            resultText = string.Empty;
                    }
                }

                resultText = resultText.Trim();
            }

            if (string.IsNullOrEmpty(resultText))
                resultText = defaultValue;

            return resultText;
        }

        public int ReadValueInt(string Section, string Key, int defaultValue = 0)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else
                return int.Parse(resultText);
        }

        public long ReadValueLong(string Section, string Key, long defaultValue = 0)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else
                return long.Parse(resultText);
        }

        public bool ReadValueBool(string Section, string Key, bool defaultValue = false)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else if (string.Compare(resultText, "false", true) == 0 || resultText == "0" || string.Compare(resultText, "no", true) == 0)
                return false;
            else if (string.Compare(resultText, "true", true) == 0 || resultText == "1" || string.Compare(resultText, "yes", true) == 0)
                return true;
            else
                return defaultValue;
        }

        public float ReadValueFloat(string Section, string Key, float defaultValue = 0)
        {
            string resultText = this.ReadValue(Section, Key, "");
            if (string.IsNullOrEmpty(resultText))
                return defaultValue;
            else
            {
                CultureInfo cultureUS = new CultureInfo("en-US");
                if (cultureUS.NumberFormat.NumberDecimalSeparator != ".")
                    cultureUS.NumberFormat.NumberDecimalSeparator = ".";

                return float.Parse(resultText, cultureUS.NumberFormat); // Make sure the decimal separator is "."
            }
        }
    }

}
