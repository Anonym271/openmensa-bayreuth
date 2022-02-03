using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenMensa_Bayreuth
{

    public static class MenuParser
    {
        private enum MealCategory { HAUPTGERICHT, BEILAGE, DESSERT, SALAT_SNACK }

        private static Price GetPriceFromTableCell(HtmlNode cell, Price.Roles role)
        {
            string className = "preise preis_typ" + role switch
            {
                Price.Roles.STUDENT => 1,
                Price.Roles.EMPLOYEE => 2,
                Price.Roles.OTHER => 3,
                _ => throw new ArgumentException(),
            };
            var priceText = cell.SelectSingleNode($"./span[@class='{className}']").GetDirectInnerText().Trim();
            double value = double.Parse(priceText[priceText.IndexOf(' ')..]);
            return new Price(role, value);
        }

        public static async Task<Canteen> GetCanteenToday(MensaType mensa)
        {
            return new Canteen(new Day[] { await GetDay(mensa, DateTime.Now) });
        }

        public static async Task<Canteen> GetCanteenThisWeek(MensaType mensa)
        {
            return new Canteen(await GetWeek(mensa, DateTime.Now));
        }

        public static async Task<Canteen> GetCanteenWeeks(MensaType mensa, DateTime startDate, int weekCount)
        {
            List<Day> days = new();
            for (int i = 0; i < weekCount; i++)
                days.AddRange(await GetWeek(mensa, startDate.AddDays(i * 7)));
            return new Canteen(days.ToArray());
        }

        public static async Task<Day> GetDay(MensaType mensa, DateTime day)
        {
            var html = await MenuGrabber.Get(mensa, day, false);
            var dayNode = html.DocumentNode.SelectSingleNode("//div[@class='tx-bwrkspeiseplan__hauptgerichte']");
            return ParseDayTable(day, dayNode);
        }

        public static async Task<Day[]> GetWeek(MensaType mensa, DateTime week)
        {
            var html = await MenuGrabber.Get(mensa, week, true);
            List<Day> days = new();

            foreach (var dayNode in html.DocumentNode.SelectNodes("//div[@class='tx-bwrkspeiseplan__bar tx-bwrkspeiseplan__hauptgerichte']"))
            {
                DateTime? date = null;
                var dateCandidates = dayNode.SelectNodes(".//a[contains(@href, 'essen/speiseplaene/bayreuth/')]");
                if (dateCandidates == null || dateCandidates.Count == 0)
                    continue;
                foreach (var dateCandidate in dateCandidates)
                {
                    var dayUrl = dateCandidate.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(dayUrl))
                    {
                        var dateString = dayUrl[(dayUrl.LastIndexOf('/') + 1) .. dayUrl.LastIndexOf('.')];
                        date = DateTime.Parse(dateString);
                        break;
                    }
                }
                if (date == null)
                    continue;
                var day = ParseDayTable((DateTime)date, dayNode);
                if (day != null)
                    days.Add(day);
            }

            return days.ToArray();
        }

        private static Day ParseDayTable(DateTime day, HtmlNode dayNode)
        {
            // Categories (Hauptgerichte, Beilagen, ...)
            List<Category> categories = new();
            var nodes = dayNode.SelectNodes("./div/div/div");
            if (nodes == null || nodes.Count == 0)
                return null;
            foreach (var categoryNode in nodes)
            {
                string categoryName;
                switch (categoryNode.GetAttributeValue("class", ""))
                {
                    case "tx-bwrkspeiseplan__hauptgerichte":
                        categoryName = "Hauptgerichte";
                        break;
                    case "tx-bwrkspeiseplan__beilagen":
                        categoryName = "Beilagen";
                        break;
                    case "tx-bwrkspeiseplan__desserts":
                        categoryName = "Desserts";
                        break;
                    case "tx-bwrkspeiseplan__salatsuppen":
                        categoryName = "Snacks, Salate";
                        break;
                    default: continue;
                }

                // Speisen (= Zeilen)
                List<Meal> meals = new();
                foreach (var row in categoryNode.SelectNodes("./table/tbody/tr"))
                {
                    var cols = row.SelectNodes("./td").ToArray();
                    var name = cols[0].GetDirectInnerText().Trim();

                    Price[] prices = new Price[] {
                        GetPriceFromTableCell(cols[1], Price.Roles.STUDENT),
                        GetPriceFromTableCell(cols[1], Price.Roles.EMPLOYEE),
                        GetPriceFromTableCell(cols[1], Price.Roles.OTHER), };

                    meals.Add(new Meal(name, prices));
                }

                categories.Add(new Category(categoryName, meals.ToArray()));
            }

            return new Day(day, categories.ToArray());
        }
    }
}
