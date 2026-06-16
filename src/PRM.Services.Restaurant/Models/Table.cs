namespace PRM.Services.Restaurant.Models;

public class Table
{
    public int TableId { get; set; }
    public int Capacity { get; set; }
    public int Status { get; set; } // 1=Available, 2=Occupied, 3=Reserved
    public DateTime CreatedAt { get; set; }
}
