using System.ComponentModel.DataAnnotations;
namespace RROMS.Models;

// Administrator profile; credentials remain in the linked account to preserve login and recovery.
public class Administrator
{
    [Key] public int AdministratorID { get; set; }
    public int CustomerID { get; set; }
    public Customer Account { get; set; } = null!;
}

public interface IAdministratorManaged
{
    int? CreatedByAdministratorID { get; set; }
    int? UpdatedByAdministratorID { get; set; }
}
