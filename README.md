# SCORM For Unity

![](https://img.shields.io/badge/2023--07--07-0.2.3-green)


Provides support for SCORM commands and build processes. Most CVRI DLX is packaged as a SCORM object and uploaded to course shells for use. See the CORE-confluence repository for developer documentation.


## Installation

### Package Manager
**SCORM** can be found in the [CORE UPM Registry](http://upm.core.varlab.org:4873/) as `com.varlab.scorm` 📦

Navigate to the **Package Manager** window in the Unity Editor and install the package under the **My Registries** sub-menu.


### Legacy Installation

In the `Packages/manifest.json` file of the Unity project, add the following line to dependencies:

> `"com.varlab.scorm": "ssh://git@bitbucket.org/VARLab/scorm.git#upm"`

Optionally, replace `upm` with a specific commit tag such as `0.2.1` to track a specific package version.


## Building SCORM Packages

The **SCORM** package provides a custom Unity `PostProcessBuildHandler` which automatically creates a `.zip` archive containing the appropriate SCORM metadata after a WebGL build is generated.

For more details and full build instructions, see the [Building for WebGL + SCORM](https://varlab-dev.atlassian.net/wiki/spaces/CV2/pages/521076742/Building+for+WebGL+SCORM) documentation on Confluence.

# Contributors

Juniper Inglis, Ujjwal Prashar, Aaron Droese, Vince Tummillo, Blake Haddaway
