using System.ComponentModel.DataAnnotations;

namespace foll_iam.IAM.Interfaces.REST.Resources;

public record DeleteAccountResource([Required] string Password);
