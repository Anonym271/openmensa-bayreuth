import logging as log
from datetime import date, datetime
from typing import Any
from bs4 import BeautifulSoup
import requests
from pyopenmensa import feed
import json
from pathlib import Path
import zipfile
from dataclasses import dataclass, asdict
import re



ARCHIVE_OLD_ENTRIES = False
JSON_DIR = Path('plans/')
JSON_INDENT = 4
ISO_DATE_RE = re.compile(r'^[12][0-9]{3}\-[01][0-9]\-[0-3][0-9]$')

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



class PlanSerializer(json.JSONEncoder):
    def default(self, o: Any) -> Any:
        if isinstance(o, (date, datetime)):
            return o.isoformat()
        if isinstance(o, (Meal, Plan)):
            return asdict(o)
        return super().default(o)

def DeserializeDate(o: dict):
    upd = {}
    for k, v in o.items():
        if isinstance(v, str):
            if ISO_DATE_RE.match(v):
                upd[k] = date.fromisoformat(v)
    o.update(upd)
    return o



@dataclass
class Meal:
    name: str
    category: str
    day: date
    notes: list[str]
    prices: dict[str, int]
    icons: list[str]


@dataclass
class Plan:
    day: date
    mensa_type: str
    meals: list[Meal]

    @staticmethod 
    def fromdict(dct: dict):
        meals = [Meal(**meal_dct) for meal_dct in dct['meals']]
        return Plan(dct['day'], dct['mensa_type'], meals)

    @staticmethod
    def load(day: date = None, mensa_type: str = None):
        return Plan.load_from(Plan.get_filename(day, mensa_type))

    @staticmethod
    def load_from(filename: str):
        with open(filename, 'r') as fin:
            return Plan.fromdict(json.load(fin, object_hook=DeserializeDate))
        
    @staticmethod
    def get_filename(day: date, mensa_type: str):
        return JSON_DIR / mensa_type / f'{day}.json'
    
    @property
    def filename(self):
        return Plan.get_filename(self.day, self.mensa_type)

    def save(self, indent=None, archive_old=None):
        if indent is None:
            indent = JSON_INDENT
        if archive_old is None:
            archive_old = ARCHIVE_OLD_ENTRIES
        json_fn = self.filename
        log.info('Saving plan %s into %s', day, json_fn)
        json_fn.parent.mkdir(exist_ok=True, parents=True)
        with open(json_fn, 'w') as fout:
            json.dump(self, fout, indent=indent, cls=PlanSerializer)
        if archive_old:
            archive_old_jsons()


def get_week_url(day_within_week: date, mensa_type='hauptmensa'):
    return f'https://www.studentenwerk-oberfranken.de/essen/speiseplaene/bayreuth/{mensa_type}/woche/{day_within_week}.html'

def get_day_url(day: date, mensa_type='hauptmensa'):
    return f'https://www.studentenwerk-oberfranken.de/essen/speiseplaene/bayreuth/{mensa_type}/{day}.html'

def get_bs(url, timeout=60):
    log.info('Requesting %s', url)
    return BeautifulSoup(requests.get(url, timeout=timeout).content, features='lxml')


def cls(c):
    return {'class': c}

def is_plan(tag):
    classes = tag.attrs.get('class')
    if not classes:
        return False
    return 'tx-bwrkspeiseplan__hauptgerichte' in classes and not 'tx-bwrkspeiseplan__bar' in classes


def parse_plan(plan: BeautifulSoup, day: date, mensa_type: str):
    """Parse a day's plan (`<div class="tx-bwrkspeiseplan__hauptgerichte">`)"""
    log.info('Parsing plan for day %s', day)
    meals = []
    for meal_tag, meal_type in meal_types.items():
        meal_table = plan.find('div', attrs=cls(meal_tag))
        if meal_table is None:
            # No meals of this type today
            log.debug('Could not find meal tag %s (%s)', meal_tag, meal_type)
            continue
        log.debug('Parsing meals for tag %s (%s)', meal_tag, meal_type)
        meal_table = meal_table.find('table', attrs=cls('tx-bwrkspeiseplan__table-meals'))
        meal_table = meal_table.find('tbody')
        meal_rows = meal_table.findChildren('tr')
        for i_row, meal_row in enumerate(meal_rows):
            log.debug('Parsing row %d of %s', i_row, meal_tag)
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
            # meal = {
            #     'name': meal_name,
            #     'category': meal_type,
            #     'date': day,
            #     'notes': notes,
            #     'prices': prices,
            #     'icons': icons,
            # }
            # meals.append(meal)
            meals.append(Meal(meal_name, meal_type, day, notes, prices, icons))
    return Plan(day, mensa_type, meals)


def parse_week(day_within_week: date, mensa_type='hauptmensa'):
    log.info('Beginning to parse week %s of mensa %s', day_within_week, mensa_type)
    bs = get_bs(get_week_url(day_within_week, mensa_type))
    plan_week = bs.find('div', attrs={'class': 'tx-bwrkspeiseplan-woche'})
    bs_headlines = plan_week.findAll('h3', attrs={'class': 'tx-bwrkspeiseplan__dayHeadline'}, recursive=False)
    bs_plans = plan_week.findAll('div', attrs={'class': 'tx-bwrkspeiseplan__hauptgerichte'}, recursive=False)
    plans = {}
    for h_date, plan in zip(bs_headlines, bs_plans):
        dt = feed.extractDate(h_date.find(text=True))
        plans[dt] = parse_plan(plan, dt, mensa_type)
    return plans



def parse_day(day: date, mensa_type='hauptmensa') -> Plan:
    log.info('Beginning to parse day %s of mensa %s', day, mensa_type)
    bs = get_bs(get_day_url(day, mensa_type))
    day_plan = bs.find('div', attrs=cls('tx-bwrkspeiseplan-tag'))
    plan = day_plan.find(is_plan)
    return parse_plan(plan, day, mensa_type)




def archive_old_jsons():
    """Scan plans directory for old plans. Plans from the current and last month are kept, 
    all other plans are archived into one zip file per month."""
    log.debug('Archiving old plans')
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
            log.warn('Archive file %s does already exist', zipfn)
            i = 2
            while zipfn.exists():
                zipfn = Path(f'{month}_{i}.zip')
                i += 1
        log.info('Archiving %d plans of month %s into archive %s', len(files), month, zipfn)
        with zipfile.ZipFile(zipfn, 'w', compression=zipfile.ZIP_DEFLATED) as zf:
            for fn in files:
                log.debug('Packing %s into %s', fn, zipfn)
                zf.write(fn)
                log.debug('Deleting %s', fn)
                fn.unlink()




# def get_plan_filename(day: date, mensa_type: str):
#     return JSON_DIR / f'{mensa_type}_{day}.json'


# def save_plan(day: date, mensa_type: str, plan: list[dict], indent=None, archive_old=None):
#     json_fn = get_plan_filename(day, mensa_type)
#     log.info('Saving plan %s into %s', day, json_fn)
#     JSON_DIR.mkdir(exist_ok=True, parents=True)
#     with open(json_fn, 'w') as fout:
#         json.dump(plan, fout, indent=indent, cls=PlanSerializer)
#     if archive_old or (archive_old is None and ARCHIVE_OLD_ENTRIES):
#         archive_old_jsons()

# def load_plan(fn):
#     with open(fn, 'r') as fin:
#         return Plan.fromdict(json.load(fin, object_hook=DeserializeDate))
    


def get_day(day: date, mensa_type='hauptmensa', use_cache=True):
    if use_cache:
        try:
            return Plan.load(day, mensa_type)
        except FileNotFoundError:
            log.info('Plan %s not found in cache, downloading current version', 
                     Plan.get_filename(day, mensa_type).name)
    plan = parse_day(day, mensa_type)
    plan.save(indent=JSON_INDENT)
    return plan



if __name__ == '__main__':
    log.getLogger().setLevel(log.DEBUG)
    week = parse_week(date.today())
    for day, plan in week.items():
        plan.save()