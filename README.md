kinect2_HSV_thresholder
=======================

GUI to threshold colors and isolate a desired range of HSV values, using the Kinect v2.

For basic color segmentation tasks, isolating a particular range of hue, saturation, and value points can be difficult and painstaking. OpenCV uses 0-179 H, 0-255 S, and 0-255 V ranges for the HSV image format, but commonly used color pickers like GIMP and Photoshop use different ranges, so picking out a shade of green from an image requires transformation and a bit of trial and error. For situations where you want to track a color in an image stream, a static image may not serve as a good representation of the noise you may encounter in an environment, and getting a really precise range is slow and error prone with the technique of sampling static images. 

Using this repo, you can filter for a specific HSV range on the fly. A simple GUI interface consisting of sliders lets you control the HSV range you want to filter. Being able to manipulate the filter in real-time gets you off the ground with color segmentation applications such as object detection faster!
