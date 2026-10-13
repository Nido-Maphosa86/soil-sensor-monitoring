# Multi-Site Smart Soil and Environmental Monitoring System (SensorGrid)

## Project Description
A multi-site monitoring system that collects soil and environmental readings from fifteen
sensors deployed across three locations (Free State, Gauteng and North West). The sensor
operator's gateway writes a reading from every sensor to Firebase every 30 minutes. The
system compares each reading with configurable thresholds for its location, monitors device
health and battery status, and raises alerts when conditions fall outside acceptable ranges.
A mobile application supports field monitoring, and a web application supports management
and analysis.

## Technologies
- Mobile: Flutter (Android)
- Web: ASP.NET MVC
- Backend: Firebase (Authentication, Realtime Database, Cloud Storage, Cloud Functions)
- Data source: third-party sensor gateway (ESP32 with 15 RS485 soil sensors) writing to Firebase

## Group Members
| Name | Role |
|---|---|
| Nido Maphosa | Tech Lead / SRS Compiler / Full-Stack Developer |
| Mbasa Tyam | Backend Developer / Frontend Design |
| Tiisetso Masita | SRS Compiler / Frontend Developer |
| Machedi Mamello | SRS Compiler |
| Caitlin Freeman | Project Manager / Frontend Developer |
| Ayatolla Pikwa | SRS Compiler / Designer |
| Katlontle Seiphetlho | Frontend Developer |
| Thobani Mjiyakho | SRS Compiler / Software Developer |

## Current Project Stage
Architecture and Design. The approved requirements (SRS v3.0) are extended in SRS v4.0
with the system architecture, use case and sequence diagrams, database design (ERD),
interface designs and navigation maps. Next stage: prototype build.
