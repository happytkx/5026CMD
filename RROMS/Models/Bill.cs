using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RROMS.Models;

public class Bill
{
    [Key]
    public int BillID { get; set; }

    public int ReservationID {  get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBill {  get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinSpend {  get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DepositApplied { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceToPay { get; set; }

    public string BillStatus { get; set; } = "Draft";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

}
