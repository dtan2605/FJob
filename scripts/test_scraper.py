#!/usr/bin/env python3

import requests
from bs4 import BeautifulSoup
from fake_useragent import UserAgent
import re

url = 'https://www.topcv.vn'
ua = UserAgent()
headers = {'User-Agent': ua.random}

print(f'Fetching {url}...')
response = requests.get(url, headers=headers, timeout=10)
soup = BeautifulSoup(response.content, 'lxml')

for script in soup(['script', 'style']):
    script.decompose()

text = soup.get_text()
cleaned = re.sub(r'\s+', ' ', text.strip())
print(f'Length: {len(cleaned)}')
print('First 500 characters:')
print(cleaned[:500])