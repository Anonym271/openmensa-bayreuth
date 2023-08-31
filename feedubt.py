import logging as log
from datetime import date, timedelta

from pyopenmensa import feed as omfeed

import parserubt as parser



meal_category_names = {
    'hauptgericht': 'Hauptgerichte',
    'beilage': 'Beilagen',
    'nachspeise': 'Nachspeisen',
    'snack_salat': 'Snacks / Salate (€/kg)',
}



def filter_prices(prices: dict):
    res = {}
    for k, v in prices.items():
        if k in omfeed.BaseBuilder.allowed_price_roles:
            res[k] = v
        else:
            log.debug("Omitting unknown price type '%s'", k)
    return res


class UbtFeedBuilder(omfeed.LazyBuilder):
    def __init__(self):
        super().__init__()

    def addPlan(self, plan: parser.Plan):
        for meal in plan.meals:
            try:
                category = meal_category_names[meal.category]
                prices = filter_prices(meal.prices)
                print(meal)
                self.addMeal(meal.day, category, meal.name, meal.notes, prices)
            except Exception as err:
                log.error('Failed to add meal "%s": %s: %s', meal.name, type(err).__name__, err)
        return self



def get_feed_day(day: date, mensa_type: str):
    """Get OpenMensa feed fot a single day"""
    feed = UbtFeedBuilder()
    try:
        feed.addPlan(parser.get_day(day, mensa_type))
    except parser.PlanNotFoundError:
        log.warning('Could not find plan %s for mensa %s. Assuming it is closed today.', day, mensa_type)
        feed.setDayClosed(day)
    return feed


def get_feed_range(start_day: date, end_day: date, mensa_type: str):
    """Get OpenMensa feed for a range of days [start_day, end_day)"""
    feed = UbtFeedBuilder()
    d = timedelta(days=1)
    day = start_day
    while day < end_day:
        # if day.weekday() == 6: # Sunday
        #     feed.setDayClosed(day)
        # else:
        #     feed.addPlan(parser.get_day(day, mensa_type))
        try:
            feed.addPlan(parser.get_day(day, mensa_type))
        except parser.PlanNotFoundError:
            log.warning('Could not find plan %s for mensa %s. Assuming it is closed today.', day, mensa_type)
            feed.setDayClosed(day)
        day = day + d