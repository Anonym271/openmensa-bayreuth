# This starts a background thread that caches the current menu regularly
import logging as log
log.debug('Initializing ubt parser module')

# HACK: I don't know how to configure gunicorn / waitress to output debug logs
log.getLogger().setLevel(log.DEBUG)
log.debug('Enabled debug log output')


from .daemonubt import cache_daemon
cache_daemon.start()