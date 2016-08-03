# Get Suspicious Transaction high frequence: 
#	interval: second
import urllib2
import json
import datetime
#10.190.172.115 for remote
print datetime.datetime.now();
data = { "interval" : 2, "amount" : 500000000 }
print data;
doc = urllib2.urlopen(url = "http://127.0.0.1/GetStatisticByFreqHttp/", data = json.dumps(data))
print doc.read();
print datetime.datetime.now();