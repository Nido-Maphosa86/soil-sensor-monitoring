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
Architecture and Design.

### Completed
**Planning and requirements**
- ✔ Stakeholder engagement and requirements gathering
- ✔ Software Requirements Specification v3.0, approved by the stakeholder
- ✔ Feasibility study, risk register, requirements change log and refined scope
- ✔ Project schedule with baseline and progress tracker
- ✔ Kick-off documents and project presentation

**Architecture and design (SRS v4.0)**
- ✔ System architecture: Flutter app, ASP.NET MVC web app, Firebase backend and sensor gateway
- ✔ Use case diagram with 26 use cases
- ✔ Seven sequence diagrams
- ✔ Database design: ERD with 13 tables and Firebase Realtime Database structure
- ✔ Security rules and access design
- ✔ Interface designs for 32 screens and dialogs (Flutter and ASP.NET) with clickable Figma prototypes
- ✔ Navigation maps for both applications
- ✔ Requirements-to-design traceability and updated project plan
