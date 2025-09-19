// Helper.cs - Utility Functions
using System;

public static class Helper
{
    public static void OpenInBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Optionally log or ignore
        }
    }
}
