namespace ComfortCare.Domain.Entities
{
    /// <summary>
    /// This class is used as an entity to hold the assignments that is not yet been assigned to an employee
    /// </summary>
    public class AssignmentEntity : IComparable<AssignmentEntity>
    {
        public int Id { get; set; }
        public DateTime TimeWindowStart { get; set; }
        public DateTime TimeWindowEnd { get; set; }
        public double Duration { get; set; }
        public DateTime ArrivalTime { get; set; }

        public int CompareTo(AssignmentEntity other)
        {
            // Assuming you want to sort by TimeWindowStart
            return TimeWindowStart.CompareTo(other.TimeWindowStart);
        }
    }
}
