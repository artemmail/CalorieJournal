using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

public class AppUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<ExternalAccount> ExternalAccounts { get; set; } = new List<ExternalAccount>();
}
