namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// User login.
    /// </summary>
    public class LoginRequest
    {
        #region Public-Members

        /// <summary>
        /// User name.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        #endregion
    }
}
