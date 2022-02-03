using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
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
                _ => throw new ArgumentException("Invalid role"),
            };
            var node = cell.SelectSingleNode($"./span[@class='{className}']");
            if (node == null)
                throw new NodeNotFoundException("Unable to find the price element for role " + role.ToString());
            var priceText = node.GetDirectInnerText().Trim();
            double value = double.Parse(priceText[priceText.IndexOf(' ')..].Replace(',', '.'), CultureInfo.InvariantCulture);
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
            if (dayNode == null)
                return null;
            return ParseDayTable(day, dayNode);
        }

        public static async Task<Day[]> GetWeek(MensaType mensa, DateTime week)
        {
            var html = await MenuGrabber.Get(mensa, week, true);
            List<Day> days = new();

            SelectAndExecuteIfPossible(html.DocumentNode, "//div[@class='tx-bwrkspeiseplan__bar tx-bwrkspeiseplan__hauptgerichte']", dayNode => {
                DateTime? date = null;
                var dateCandidates = dayNode.SelectNodes(".//a[contains(@href, 'essen/speiseplaene/bayreuth/')]");
                if (dateCandidates == null || dateCandidates.Count == 0)
                    return;
                foreach (var dateCandidate in dateCandidates)
                {
                    var dayUrl = dateCandidate.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(dayUrl))
                    {
                        var dateString = dayUrl[(dayUrl.LastIndexOf('/') + 1)..dayUrl.LastIndexOf('.')];
                        date = DateTime.Parse(dateString);
                        break;
                    }
                }
                if (date == null)
                    return;
                var day = ParseDayTable((DateTime)date, dayNode);
                if (day != null)
                    days.Add(day);
            });


            return days.ToArray();
        }

        private static Day ParseDayTable(DateTime day, HtmlNode dayNode)
        {
            // Categories (Hauptgerichte, Beilagen, ...)
            List<Category> categories = new();
            var nodes = dayNode.SelectNodes("./div/div/div");
            SelectAndExecuteIfPossible(dayNode, "./div/div/div", categoryNode => {
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
                    default: return;
                }

                // Speisen (= Zeilen)
                var meals = new List<Meal>();

                SelectAndExecuteIfPossible(categoryNode, "./table/tbody/tr", row => {
                    var cols = row.SelectNodes("./td").ToArray();
                    var name = cols[0].GetDirectInnerText().Trim();

                    var prices = new List<Price>();
                    foreach (var role in new Price.Roles[] { Price.Roles.STUDENT, Price.Roles.EMPLOYEE, Price.Roles.OTHER })
                    {
                        try
                        {
                            prices.Add(GetPriceFromTableCell(cols[1], role));
                        }
                        catch (Exception) { }
                    }

                    meals.Add(new Meal(name, prices.ToArray()));
                });

                categories.Add(new Category(categoryName, meals.ToArray()));
            });

            return new Day(day, categories.ToArray());
        }

        private static void SelectAndExecuteIfPossible(HtmlNode node, string xpath, Action<HtmlNode> action)
        {
            var result = node.SelectNodes(xpath);
            if (result == null || result.Count == 0)
                return;
            foreach (var child in result)
                action(child);
        }
    }
}
