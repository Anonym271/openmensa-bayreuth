import logging as log
import os
import urllib
from datetime import date, timedelta

from flask import Flask, jsonify, make_response, request, url_for

import feedubt



known_canteens = [
    'hauptmensa',
    'frischraum',
    'gsp',
    'medizin-campus',
    'mensa-fan',
    'nuernberger-strasse',
]

# Shamelessly copied from https://github.com/f4lco/om-parser-stw-potsdam-v2/blob/master/stw_potsdam/views.py
app = Flask(__name__)
app.url_map.strict_slashes = False

if 'BASE_URL' in os.environ:  # pragma: no cover
    base_url = urllib.parse.urlparse(os.environ.get('BASE_URL'))
    if base_url.scheme:
        app.config['PREFERRED_URL_SCHEME'] = base_url.scheme
    if base_url.netloc:
        app.config['SERVER_NAME'] = base_url.netloc
    if base_url.path:
        app.config['APPLICATION_ROOT'] = base_url.path



def unknown_canteen(name):
    return make_response(f'ERROR: unknown canteen "{name}"', 400)


@app.route('/canteens/<canteen_name>/feed/day')
def get_feed_day(canteen_name: str):
    """Get OpenMensa XML-Feed of Mensa mensa_type for the current day or the day defined by the url parameter 'date'."""
    if canteen_name not in known_canteens:
        return unknown_canteen(canteen_name)
    try:        
        day = request.args.get('date')
        try:
            day = date.today() if day is None else date.fromisoformat(day)
        except ValueError:
            return make_response(f'ERROR: failed to parse date', 400)
        return feedubt.get_feed_day(day, canteen_name).toXMLFeed()
    except Exception as err:
        # log.error('Failed to fetch today\'s feed: %s: %s', type(err).__name__, err)
        log.exception('Failed to fetch today\'s feed')
        return make_response("ERROR: Failed to fetch today's feed! Please contact the Mensa's OpenMensa admin or see the log files.", 500)


@app.route('/canteens/<canteen_name>/feed/complete')
def get_feed_complete(canteen_name: str):
    """Get complete OpenMensa XML-Feed of Mensa mensa_type. 
    Default range is [today - 21, today + 21), i.e. the last three weeks and 
    the next three weeks. Range can be changed with optional url parameters 'start', 'stop'."""
    if canteen_name not in known_canteens:
        return unknown_canteen(canteen_name)
    try:
        start_day = request.args.get('start')
        stop_day = request.args.get('stop')
        try:
            start_day = date.today() - timedelta(days=21) if start_day is None else date.fromisoformat(start_day)
            stop_day = date.today() + timedelta(days=21) if stop_day is None else date.fromisoformat(stop_day)
        except ValueError:
            return make_response(f'ERROR: failed to parse start or stop date', 400)
        return feedubt.get_feed_day(date.today(), canteen_name).toXMLFeed()
    except Exception as err:
        # log.error('Failed to fetch ranged feed (%s - %s): %s: %s', start_day, stop_day, type(err).__name__, err)
        log.exception('Failed to fetch ranged feed (%s - %s)', start_day, stop_day)
        return make_response("ERROR: Failed to fetch mensa feed! Please contact the Mensa's OpenMensa admin or see the log files.", 500)


if __name__ == '__main__':
    log.getLogger().setLevel(log.DEBUG)
    app.run()