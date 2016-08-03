# Get default Suspicious Transaction, condition: 
#	amount>50BTC, 
#	transaction to same address1s in 1s,
#	cycle like A=>B=>A and amount>50BTC
import urllib2
import json
#10.190.172.115 for remote
doc = urllib2.urlopen(url = "http://127.0.0.1/GetStatisticByDefault/")
print doc.read();