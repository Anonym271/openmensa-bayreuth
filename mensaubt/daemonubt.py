import logging as log
import threading
import time
from datetime import date, timedelta, datetime
from . import parserubt as parser



def fetch_day(day, mensa):
    log.debug('Cache daemon fetching day %s of mensa %s', day, mensa)
    try:
        parser.fetch_day(day, mensa)
    except:
        log.exception('Cache daemon failed to fetch day %s of mensa %s', day, mensa)


def fetch_week(week, mensa):
    log.debug('Cache daemon fetching week %s of mensa %s', week, mensa)
    try:
        parser.fetch_week(week, mensa)
    except:
        log.exception('Cache daemon failed to fetch week %s of mensa %s', week, mensa)


def do_today_fetch():
    log.info('Cache daemon starting fetch of today\'s week')
    today = date.today()
    for mensa in parser.known_canteens:
        fetch_week(today, mensa)


def do_big_fetch():
    log.info('Cache daemon starting big fetch')
    today = date.today()
    for mensa in parser.known_canteens:
        for i in range(-1, 5):
            week = today + timedelta(weeks=i)
            fetch_week(week, mensa)


def cache_thread_main():
    # On start: load last and next 4 weeks
    log.info('Starting cache daemon. Fetching last and next 4 weeks of all canteens.')
    today = date.today()
    for mensa in parser.known_canteens:
        for i in range(-4, 5):
            week = today + timedelta(weeks=i)
            fetch_week(week, mensa)
    # Then: update current week hourly, update next weeks daily at midnight
    next_today_fetch = datetime.now() + timedelta(hours=1)
    next_big_fetch = datetime.combine(date.today() + timedelta(days=1), datetime.min.time())
    while True:
        if datetime.now() >= next_today_fetch:
            do_today_fetch()
            next_today_fetch += timedelta(hours=1)
        if datetime.now() >= next_big_fetch:
            do_big_fetch()
            next_big_fetch += timedelta(days=1)
        next_stop = min(next_today_fetch, next_big_fetch)
        td = next_stop - datetime.now()
        time.sleep(td.total_seconds())



cache_daemon = threading.Thread(target=cache_thread_main, name='cache_worker')
cache_daemon.daemon = True