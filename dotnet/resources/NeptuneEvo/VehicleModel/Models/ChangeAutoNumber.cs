namespace NeptuneEvo.VehicleModel.Models
{
    /// <summary>
    /// Vehicle data
    /// </summary>
    public class VehicleInfo
    {
        /// <summary>
        /// Vehicle class
        /// </summary>
        public string Class;
        /// <summary>
        /// Maximum number of inventory slots
        /// </summary>
        public int MaxSlots;
        /// <summary>
        /// Price
        /// </summary>
        public int Price;
        /// <summary>
        /// Name
        /// </summary>
        public string Name;


        public VehicleInfo(string Class, int MaxSlots, int Price, string Name = null)
        {
            this.Class = Class;
            this.MaxSlots = MaxSlots;
            this.Price = Price;
            this.Name = Name;
        }
    }
}
