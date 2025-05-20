using System.Collections.Generic;
using System.Linq;
using MimeKit;

namespace ASC.Mail.Core.Utils
{
    public static class MailAddressHelper
    {
        public static List<string> ParseAddresses(string rawAddresses)
        {
            if (string.IsNullOrWhiteSpace(rawAddresses))
                return new List<string>();

            try
            {
                return InternetAddressList.Parse(rawAddresses)
                    .Mailboxes
                    .Select(mb => mb.Address.ToLowerInvariant())
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
