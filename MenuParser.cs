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

        //public static async Task<Canteen> GetCanteenDay(MensaType mensa, DateTime day)
        //{
        //    return new Canteen(new Day[] { await GetToday(mensa, day) });
        //}
        public static async Task<Canteen> GetCanteenToday(MensaType mensa) //=> await GetCanteenDay(mensa, DateTime.Now);
        {
            var html = MenuGrabber.Instance.GetToday(mensa);
            return new Canteen(new Day[] { await GetDay(html, DateTime.Now) });
        }

        public static async Task<Canteen> GetCanteenThisWeek(MensaType mensa)
        {
            return new Canteen(await GetWeek(MenuGrabber.Instance.GetWeekly(mensa)[0]));
        }

        public static async Task<Canteen> GetCanteenWeeks(MensaType mensa, DateTime startDate, int weekCount)
        {
            //List<Day> days = new();
            //for (int i = 0; i < weekCount; i++)
            //    days.AddRange(await GetWeek(mensa, startDate.AddDays(i * 7)));
            //return new Canteen(days.ToArray());
            var weekHtmls = MenuGrabber.Instance.GetWeekly(mensa);
            var tasks = new Task<Day[]>[3];
            for (int i = 0; i < 3; i++)
                tasks[i] = GetWeek(weekHtmls[i]);
            var days = new List<Day>();
            foreach (var t in tasks)
                days.AddRange(await t);
            return new Canteen(days.ToArray());
        }

        //public static async Task<Day> GetToday(MensaType mensa, DateTime day)
        public static async Task<Day> GetDay(HtmlDocument dayHtml, DateTime day)
        {
            //var html = await MenuGrabber.Get(mensa, day, false);
            var lunchNode = dayHtml.DocumentNode.SelectSingleNode("//div[@class='tx-bwrkspeiseplan__hauptgerichte']");
            var dinnerNode = dayHtml.DocumentNode.SelectSingleNode("//div[@class='tx-bwrkspeiseplan__abendkarte']");
            var categories = new List<Category>();
            if (lunchNode != null)
                categories.AddRange(ParseDayTable(lunchNode));
            if (dinnerNode != null)
                categories.AddRange(ParseDayTable(dinnerNode));
            return new Day(day, categories.ToArray());
        }

        //public static async Task<Day[]> GetWeek(MensaType mensa, DateTime week)
        public static async Task<Day[]> GetWeek(HtmlDocument weekHtml)
        {
           // var weekHtml = await MenuGrabber.Get(mensa, week, true);
            List<Day> days = new();

            SelectAndExecuteIfPossible(weekHtml.DocumentNode, "//div[@class='tx-bwrkspeiseplan__bar tx-bwrkspeiseplan__hauptgerichte']", dayNode => {
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
                days.Add(new Day((DateTime)date, ParseDayTable(dayNode).ToArray()));
            });

            return days.ToArray();
        }

        private static List<Category> ParseDayTable(HtmlNode dayNode)
        {
            // Categories (Hauptgerichte, Beilagen, ...)
            List<Category> categories = new();
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
                        categoryName = "Snacks, Salate (€/1kg)";
                        break;
                    case "tx-bwrkspeiseplan__abendkarte":
                        categoryName = "Abendkarte (ab 16:00)";
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

            return categories;
            //return new Day(day, categories.ToArray());
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
