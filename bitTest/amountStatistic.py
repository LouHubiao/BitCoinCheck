import urllib2
import json
import datetime
#10.190.172.115 for remote
print datetime.datetime.now();
data = { "amount" : 5000000000 }
print data;
doc = urllib2.urlopen(url = "http://127.0.0.1/GetStatisticByAmountHttp/", data = json.dumps(data))
print doc.read();
print datetime.datetime.now();