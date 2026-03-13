namespace Easshas.Domain.ValueObjects
{
    public class Address
    {
        public string FullName { get; private set; }
        public string Line1 { get; private set; }
        public string? Line2 { get; private set; }
        public string City { get; private set; }
        public string State { get; private set; }
        public string Country { get; private set; }
        public string PostalCode { get; private set; }
        public string PhoneNumber { get; private set; }

        private Address()
        {
            FullName = string.Empty;
            Line1 = string.Empty;
            City = string.Empty;
            State = string.Empty;
            Country = string.Empty;
            PostalCode = string.Empty;
            PhoneNumber = string.Empty;
        }

        public Address(string fullName, string line1, string? line2, string city, string state, string country, string postalCode, string phoneNumber)
        {
            FullName = fullName;
            Line1 = line1;
            Line2 = line2;
            City = city;
            State = state;
            Country = country;
            PostalCode = postalCode;
            PhoneNumber = phoneNumber;
        }
    }
}
