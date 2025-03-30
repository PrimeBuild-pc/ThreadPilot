namespace ThreadPilot.Models
{
    /// <summary>
    /// CPU core information
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Core ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Core name
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Core usage (percentage)
        /// </summary>
        public double Usage { get; set; }
        
        /// <summary>
        /// Core temperature (Celsius)
        /// </summary>
        public double Temperature { get; set; }
        
        /// <summary>
        /// Core frequency (MHz)
        /// </summary>
        public double Frequency { get; set; }
        
        /// <summary>
        /// Is core parked
        /// </summary>
        public bool IsParked { get; set; }
    }
}