namespace BlobBackupLib.Azure.Model
{
    public class AppSettings
    {
        public int ConsoleWidth { get; set; }
        public int ConsoleHeight { get; set; }
        public LoginCredentialsConfiguration LoginCredentials { get; set; }
        public DatabaseConfiguration DataBase { get; set; }
        public GeneralConfiguration General { get; set; }
    }
}
