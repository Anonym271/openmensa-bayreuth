from datetime import date, datetime
from typing import Any
from bs4 import BeautifulSoup
import requests
from pyopenmensa import feed
import json
from pathlib import Path
import zipfile



JSON_DIR = Path('plans/')


meal_types = {
    'tx-bwrkspeiseplan__hauptgerichte': 'hauptgericht',
    'tx-bwrkspeiseplan__beilagen': 'beilage',
    'tx-bwrkspeiseplan__desserts': 'nachspeise',
    'tx-bwrkspeiseplan__salatsuppen': 'snack_salat',
}

price_types = {
    'preis_typ1': 'students',
    'preis_typ2': 'employees',
    'preis_typ3': 'other',
    'preis_typ4': 'special', # Not in official OpenMensaFeed
}




def get_week_url(day_within_week: date, mensa_type='hauptmensa'):
    return f'https://www.studentenwerk-oberfranken.de/essen/speiseplaene/bayreuth/{mensa_type}/woche/{day_within_week}.html'

def get_day_url(day: date, mensa_type='hauptmensa'):
    return f'https://www.studentenwerk-oberfranken.de/essen/speiseplaene/bayreuth/{mensa_type}/{day}.html'

def get_bs(url, timeout=60):
    return BeautifulSoup(requests.get(url, timeout=timeout).content)


def cls(c):
    return {'class': c}

def is_plan(tag):
    classes = tag.attrs.get('class')
    if not classes:
        return False
    return 'tx-bwrkspeiseplan__hauptgerichte' in classes and not 'tx-bwrkspeiseplan__bar' in classes


def parse_plan(plan: BeautifulSoup, day: date):
    """Parse a day's plan (`<div class="tx-bwrkspeiseplan__hauptgerichte">`)"""
    meals = []
    for meal_tag, meal_type in meal_types.items():
        meal_table = plan.find('div', attrs=cls(meal_tag))
        if meal_table is None:
            # No meals of this type today
            continue
        meal_table = meal_table.find('table', attrs=cls('tx-bwrkspeiseplan__table-meals'))
        meal_table = meal_table.find('tbody')
        meal_rows = meal_table.findChildren('tr')
        for meal_row in meal_rows:
            cols = meal_row.findChildren('td', recursive=False)
            assert len(cols) == 3
            c_name, c_price, c_icons = cols
            meal_name = c_name.find(text=True).strip()
            sup = c_name.find('sup')
            if sup is not None:
                sup = sup.find(text=True).strip(' \t\r\n(),')
                notes = [note.strip() for note in sup.split(',')]
            else:
                notes = []
            prices = {}
            for price_tag, price_type in price_types.items():
                p = c_price.find(attrs=cls(price_tag)).find(text=True)
                prices[price_type] = p
            prices = feed.buildPrices(prices)
            icons = [icon_img.attrs['src'] for icon_img in c_icons.findChildren('img')]
            icons += [' '.join(icon_icon.attrs['class']) for icon_icon in c_icons.findChildren('i', attrs=cls('icon'))]
            meal = {
                'name': meal_name,
                'category': meal_type,
                'date': day,
                'notes': notes,
                'prices': prices,
                'icons': icons,
            }
            meals.append(meal)
    return meals


def parse_week(day_within_week: date, mensa_type='hauptmensa'):
    bs = get_bs(get_week_url(day_within_week, mensa_type))
    plan_week = bs.find('div', attrs={'class': 'tx-bwrkspeiseplan-woche'})
    bs_headlines = plan_week.findAll('h3', attrs={'class': 'tx-bwrkspeiseplan__dayHeadline'}, recursive=False)
    bs_plans = plan_week.findAll('div', attrs={'class': 'tx-bwrkspeiseplan__hauptgerichte'}, recursive=False)
    plans = {}
    for h_date, plan in zip(bs_headlines, bs_plans):
        dt = feed.extractDate(h_date.find(text=True))
        plans[dt] = parse_plan(plan, dt)
    return plans



def parse_day(day: date, mensa_type='hauptmensa'):
    bs = get_bs(get_day_url(day, mensa_type))
    day_plan = bs.find('div', attrs=cls('tx-bwrkspeiseplan-tag'))
    plan = day_plan.find(is_plan)
    return parse_plan(plan, day)




def archive_old_jsons():
    if not JSON_DIR.exists():
        return
    today = date.today()
    months_collected = {}
    for fn in JSON_DIR.glob('*.json'):
        n = fn.with_suffix('').name
        file_date = datetime.strptime(n, '%Y-%m-%d').date()
        if today != file_date:
            if file_date.year == today.year:
                if today.month - file_date.month > 1:
                    months_collected.setdefault(file_date.strftime('%Y-%m'), []).append(fn)
            elif file_date.year < today.year:
                if (today.month + 12 - file_date.month) > 1:
                    months_collected.setdefault(file_date.strftime('%Y-%m'), []).append(fn)
    for month, files in months_collected.items():
        zipfn = Path(f'{month}.zip')
        if zipfn.exists():
            print('WARNING: archive file', zipfn, 'does already exist')
            i = 2
            while zipfn.exists():
                zipfn = Path(f'{month}_{i}.zip')
        with zipfile.ZipFile(zipfn, 'w', compression=zipfile.ZIP_DEFLATED) as zf:
            for fn in files:
                zf.write(fn)
                fn.unlink()


class PlanSerializer(json.JSONEncoder):
    def default(self, o: Any) -> Any:
        if isinstance(o, (date, datetime)):
            return o.isoformat()

def DeserializeDate(o):
    if isinstance(o, dict) and 'date' in o:
        o['date'] = date.fromisoformat(o['date'])
        return o


def save_plan(day: date, plan: list[dict], indent=None, archive_old=True):
    JSON_DIR.mkdir(exist_ok=True, parents=True)
    with open(JSON_DIR / f'{day}.json', 'w') as fout:
        json.dump(plan, fout, indent=indent, cls=PlanSerializer)
        
    if archive_old:
        archive_old_jsons()





if __name__ == '__main__':
    week = parse_week(date.today())
    for day, plan in week.items():
        save_plan(day, plan, indent=4)