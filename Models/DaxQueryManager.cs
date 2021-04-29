using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AnalysisServices.AdomdClient;

namespace ProgrammingDatasets.Models {

  class DaxQueryManager {

    public static void ConvertDaxQueryToCsv(string DatasetName, string DaxQuery, string FileName) {

      string workspaceConnection = AppSettings.WorkspaceConnection;
      string accessToken = TokenManager.GetAccessToken(PowerBiPermissionScopes.TenantProvisioning);
      string connectStringUser = $"DataSource={workspaceConnection};Password={accessToken};Catalog={DatasetName};";
      AdomdConnection adomdConnection = new AdomdConnection(connectStringUser);
      adomdConnection.Open();

      Console.WriteLine($"Executing DAX Query on {DatasetName} dataset");
      Console.WriteLine("---------------------------------------------------------------");
      Console.WriteLine(DaxQuery);
      Console.WriteLine("---------------------------------------------------------------");
      Console.WriteLine();

      AdomdCommand adomdCommand = new AdomdCommand(DaxQuery, adomdConnection);
      AdomdDataReader reader = adomdCommand.ExecuteReader();

      ConvertReaderToCsv(FileName, reader);

      reader.Dispose();
      adomdConnection.Close();

    }

    private static void ConvertReaderToCsv(string FileName, AdomdDataReader Reader, bool OpenInExcel = true) {
      string csv = string.Empty;

      // create a loop to determine columns
      for (int col = 0; col < Reader.FieldCount; col++) {
        csv += Reader.GetName(col);
        csv += (col < (Reader.FieldCount - 1)) ? "," : "\n";
      }

      // Create a loop for every row in the resultset
      while (Reader.Read()) {
        // Create a loop for every column in the current row
        for (int i = 0; i < Reader.FieldCount; i++) {
          csv += Reader.GetValue(i);
          csv += (i < (Reader.FieldCount - 1)) ? "," : "\n";
        }
      }

      string filePath = System.IO.Directory.GetCurrentDirectory() + @"\" + FileName;
      StreamWriter writer = File.CreateText(filePath);
      writer.Write(csv);
      writer.Flush();
      writer.Dispose();

      Console.WriteLine("Display query results in CSV format");
      Console.WriteLine("-----------------------------------");
      Console.WriteLine(csv);

      if (OpenInExcel) {
        OpenCsvInExcel(filePath);
      }

    }

    private static void OpenCsvInExcel(string FilePath) {

      ProcessStartInfo startInfo = new ProcessStartInfo();

      bool excelFound = false;
      if (File.Exists("C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.EXE")) {
        startInfo.FileName = "C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.EXE";
        excelFound = true;
      }
      else {
        if (File.Exists("C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\EXCEL.EXE")) {
          startInfo.FileName = "C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\EXCEL.EXE";
          excelFound = true;
        }
      }
      if (excelFound) {
        startInfo.Arguments = FilePath;
        Process.Start(startInfo);
      }
      else {
        System.Console.WriteLine("Coud not find Microsoft Exce on this PC.");
      }

    }




  }



}
