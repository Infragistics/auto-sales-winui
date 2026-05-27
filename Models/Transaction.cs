using System;

namespace AutoSales.Models
{
    public class Transaction
    {
        public Transaction() { }

        public Transaction(string model, int quantity, double totalCost, double percent,
            string dealer, string region, string city, DateTime date)
        {
            Model = ProductPerformance.CorrectModelName(model);
            Quantity = quantity;
            TotalCost = totalCost;
            Percent = percent;
            Dealer = dealer;
            Region = region;
            City = city;
            Date = date;
        }

        public string Model { get; set; }
        public int Quantity { get; set; }
        public double TotalCost { get; set; }
        public double Percent { get; set; }
        public string Dealer { get; set; }
        public string Region { get; set; }
        public string City { get; set; }
        public DateTime Date { get; set; }
    }
}
