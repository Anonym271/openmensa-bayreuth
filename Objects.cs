using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace OpenMensa_Bayreuth
{
    [XmlRoot("openmensa", Namespace = "http://openmensa.org/open-mensa-v2", IsNullable = false)]
    public class OpenMensa
    {
        private const string XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance";

        [XmlAttribute(AttributeName = "schemaLocation", Namespace = XSI_NAMESPACE)]
        public string SchemaLocation = "http://openmensa.org/open-mensa-v2 http://openmensa.org/open-mensa-v2.xsd";
        [XmlAttribute(AttributeName = "version")]
        public string openmensaVersion = "2.0";

        [XmlElement(ElementName = "version")]
        public string parserVersion;
        public Canteen canteen;

        public OpenMensa() => this.parserVersion = "1.0";
        public OpenMensa(Canteen canteen, string version = "1.0")
        {
            this.parserVersion = version;
            this.canteen = canteen;
        }
    }

    public class Canteen
    {
        [XmlElement(ElementName = "day")]
        public Day[] days;

        public Canteen() { }
        public Canteen(Day[] days) => this.days = days;
    }

    public class Day
    {
        [XmlAttribute]
        public string date;

        [XmlElement(ElementName = "category")]
        public Category[] categories;

        public Day() {}
        public Day(DateTime date, Category[] categories)
        {
            this.date = date.ToString("yyyy-MM-dd");
            this.categories = categories;
        }
    }

    public class Category
    {
        [XmlAttribute]
        public string name;

        [XmlElement(ElementName = "meal")]
        public Meal[] meals;

        public Category() { }
        public Category(string name, Meal[] meals)
        {
            this.name = name;
            this.meals = meals;
        }
    }

    public class Meal
    {
        public string name;
      //  [XmlElement(IsNullable = true)]
        public string note;
        [XmlElement(ElementName = "price")]
        public Price[] prices;

        public Meal() { }
        public Meal(string name, Price[] prices, string note = null)
        {
            this.name = name;
            this.prices = prices;
            this.note = note;
        }
    }
    
    public class Price
    {
        public enum Roles { STUDENT, EMPLOYEE, OTHER };

        [XmlAttribute(AttributeName = "role")]
        public string role;

        [XmlText]
        public double value;

        public Price() { }
        public Price(Roles role, double value)
        {
            switch (role)
            {
                case Roles.STUDENT:
                    this.role = "student";
                    break;
                case Roles.EMPLOYEE:
                    this.role = "employee";
                    break;
                case Roles.OTHER:
                    this.role = "other";
                    break;
            }
            this.value = value;
        }
    }
}
