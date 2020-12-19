using BlobBackupLib.Database.Model;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlobBackupLib.Jobs
{
    public static class CsvHandler
    {
        public static List<Blob> CsvToList(string csv)
        {
            var files = new List<Blob>();
            string[] lines = csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var values = line.Split(';');
                if (string.IsNullOrWhiteSpace(values[0]))
                {
                    continue;
                }

                string targetFilename = "";

                if(values.Length > 1 && !string.IsNullOrWhiteSpace(values[1]))
                {
                    targetFilename = values[1];
                }
               
                files.Add(new Blob(){ FullPathFilename = values[0], TargetFilename = targetFilename });

            }
            
            return files;
        }

        public static bool IsValidCsv(string csv)
        {

            using (var parser = new TextFieldParser(new StringReader(csv)))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                string[] line;
                while (!parser.EndOfData)
                {
                    try
                    {
                        line = parser.ReadFields();
                    }
                    catch (MalformedLineException ex)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

    }
}
