import sys
if sys.version_info < (3, 3):
    sys.stdout.write("This app requires Python 3.3 or higher")
    sys.exit(1)

import flask
import threading
from datetime import datetime
from json import dumps
from time import sleep

app = flask.Flask(__name__)

servers = {}

@app.route('/', methods=['GET', 'POST'])
def index():
    global servers
    if flask.request.method == 'POST':
        port = str(flask.request.data, 'utf-8')

        if not port.isdigit():
            return '403'

        ip = "{0}:{1}".format(flask.request.remote_addr, port)
        servers[ip] = datetime.now()
        return '200'
    else:
        return dumps({"list": list(servers.keys())})

def checkThread():
    print('cleaning list...')
    for server in dict(servers):
        date = servers[server]
        if (datetime.now() - date).total_seconds() > 6*60:
            del servers[server]

    sleep(6*60)
    checkThread()


if __name__ == '__main__':
    t = threading.Thread(target=checkThread)
    t.daemon = True
    t.start()

    app.debug = False #InDev ONLY
    #serve(app, port=int(environ['PORT'])) #For deployment
    app.run() #Run our app. #InDev ONLY