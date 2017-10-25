import json
import requests

resp = requests.post('http://localhost:5000/', data='4498')
print(resp)