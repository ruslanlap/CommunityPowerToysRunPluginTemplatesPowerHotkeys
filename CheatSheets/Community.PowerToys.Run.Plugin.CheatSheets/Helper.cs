   // Helper.cs - Utility Functions
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
                // Fail silently
            }
        }
    }