namespace PerformanceStatistics.Monitoring
{
    /// <summary>
    /// One TCP connection.
    /// </summary>
    public class TcpConnectionSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Local endpoint (address:port).
        /// </summary>
        public string Local { get; set; } = string.Empty;

        /// <summary>
        /// Remote endpoint (address:port).
        /// </summary>
        public string Remote { get; set; } = string.Empty;

        /// <summary>
        /// State, e.g. Established or TimeWait.
        /// </summary>
        public string State { get; set; } = string.Empty;

        #endregion
    }
}
