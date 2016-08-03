#cost too much time...
import urllib2
import json
#10.190.172.115 for remote
data = { "cycleCount" : 2, "amount" : 5000000000 }
print data;
doc = urllib2.urlopen(url = "http://10.190.172.115/GetStatisticByCycleHttp/", data = json.dumps(data))
print doc.read();