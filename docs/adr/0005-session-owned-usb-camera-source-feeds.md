# Session-Owned USB Camera Source Feeds

Live physical Camera Sources use one session-owned feed owner that is shared by camera tile viewing and Vision Pipeline processing consumers, because opening the same USB device through multiple `VideoCapture` instances can halt other live feeds. File-based camera sources remain unchanged for now because independent file playback already works and does not have the same physical device ownership constraint.
