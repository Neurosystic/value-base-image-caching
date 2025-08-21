<!-- README template obtained from: https://github.com/othneildrew/Best-README-Template/blob/master/README.md?plain=1-->
[![LinkedIn][linkedin-shield]](https://www.linkedin.com/in/xinyu-chen-482142144/)
<span id="readme-top"></span>


<!-- PROJECT LOGO -->
<br />
<div align="center">
  <h3 align="center" id="readme-top">Image File Value-Based Caching System</h3>

  <p align="center">
     An image file cache system that utilised the value-based web caching technique to allow clients to download images stored server-side efficiently
    <br />
  </p>
</div>
<br />

<!-- ABOUT THE PROJECT -->
## About The Project
<p>The solution is designed to demonstrate the client, cache proxy, and server component across a network. </p>
<ul>
  <li>Engineered client, cache, and server interfaces which utilised the WPF UI framework</li>
  <li>Implemented Transmission Control Protocol <strong>(TCP)</strong> through <strong>socket programming</strong> in C# and <strong>.NET 7 Core framework</strong>, which allowed client, cache, and server connections to be established</li>
  <li>Applied <strong>Rabin function</strong> to partition files into blocks, used for value-based web caching and utilised <strong>MD5 digest algorithm</strong> to create message digests</li>
</ul>

<br />
<p>Client is able to connect to the server and select file(s) to be downloaded to the client machine</p>
<img width="1919" height="548" alt="Screenshot 2025-08-21 213654" src="https://github.com/user-attachments/assets/48616cb5-abbc-40ba-bd1e-2afdfd74af43" />

<br /> 
<p>Server receives request from client and logs server activity</p>
<img width="1919" height="552" alt="Screenshot 2025-08-21 213708" src="https://github.com/user-attachments/assets/dc19f744-81f8-473f-bfd7-ec9ad85c257f" />

<br />
<p>Client receive preview of selected image</p>
<img width="1917" height="554" alt="Screenshot 2025-08-21 214241" src="https://github.com/user-attachments/assets/56349aa3-c547-446c-96fc-928a62a2a5ea" />

<br />
<p>Cache proxy designed to be able to view data fragments and clear caches</p>
<img width="981" height="546" alt="Screenshot 2025-08-21 214252" src="https://github.com/user-attachments/assets/e553e814-2909-4a6d-8b84-b97399aa0afb" />

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[linkedin-shield]: https://img.shields.io/badge/-LinkedIn-black.svg?style=for-the-badge&logo=linkedin&colorB=555
