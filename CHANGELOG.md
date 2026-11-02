v1.0.1
- Add LuaDnsCnameDomainValidator (validation type "cname") so CAs that require CNAME domain control validation (e.g. CSC Global) can have their record published in LuaDNS
- Share credentials/zone discovery between the TXT (dns-01) and CNAME validators via a common base class; the existing dns-01 validator is unchanged

v1.0.0
- Inital Version
