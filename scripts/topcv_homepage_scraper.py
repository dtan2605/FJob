#!/usr/bin/env python3
"""
Script để cào toàn bộ text từ trang chủ TopCV.vn
Có thể chạy standalone hoặc trong Docker container
"""

import requests
from bs4 import BeautifulSoup
from fake_useragent import UserAgent
import re
import sys
import json
from pathlib import Path
from datetime import datetime


def clean_text(text: str) -> str:
    """Clean extracted text by removing extra whitespace and normalizing"""
    # Remove extra whitespace and newlines
    text = re.sub(r'\s+', ' ', text.strip())
    # Remove non-breaking spaces
    text = text.replace('\u00a0', ' ')
    # Remove multiple spaces
    text = re.sub(r' +', ' ', text)
    return text.strip()


def extract_structured_data(soup: BeautifulSoup) -> dict:
    """Extract structured data from common HTML elements"""
    data = {
        "title": soup.title.string if soup.title else "",
        "headings": {
            "h1": [h.get_text().strip() for h in soup.find_all('h1')],
            "h2": [h.get_text().strip() for h in soup.find_all('h2')],
            "h3": [h.get_text().strip() for h in soup.find_all('h3')],
        },
        "links": [{"text": a.get_text().strip(), "href": a.get('href')} for a in soup.find_all('a', href=True)][:20],  # Limit to first 20 links
        "meta_description": "",
        "meta_keywords": "",
    }

    # Extract meta tags
    meta_desc = soup.find('meta', attrs={'name': 'description'})
    if meta_desc:
        data["meta_description"] = meta_desc.get('content', '')

    meta_keywords = soup.find('meta', attrs={'name': 'keywords'})
    if meta_keywords:
        data["meta_keywords"] = meta_keywords.get('content', '')

    return data


def extract_all_text_from_topcv(save_to_file: bool = False, output_format: str = "text") -> str:
    """Extract all visible text from TopCV.vn homepage"""
    url = "https://www.topcv.vn"
    ua = UserAgent()

    headers = {
        'User-Agent': ua.random,
        'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
        'Accept-Language': 'vi,en-US;q=0.7,en;q=0.3',
        # Remove Accept-Encoding to avoid compression issues
        'Connection': 'keep-alive',
        'Upgrade-Insecure-Requests': '1',
    }

    try:
        print(f"Fetching {url}...")
        response = requests.get(url, headers=headers, timeout=30)
        response.raise_for_status()

        print("Parsing HTML...")
        try:
            soup = BeautifulSoup(response.content, 'lxml')
        except Exception:
            print("Warning: lxml parser unavailable, falling back to built-in html.parser.")
            soup = BeautifulSoup(response.content, 'html.parser')

        # Remove script and style elements
        for script in soup(["script", "style"]):
            script.decompose()

        # Extract text from all elements
        text = soup.get_text()

        # Clean the text
        cleaned_text = clean_text(text)

        print(f"Extracted {len(cleaned_text)} characters of text")

        # Extract structured data
        structured_data = extract_structured_data(soup)

        if save_to_file:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            base_filename = f"topcv_homepage_{timestamp}"

            if output_format == "json":
                output_data = {
                    "url": url,
                    "timestamp": datetime.now().isoformat(),
                    "text": cleaned_text,
                    "structured_data": structured_data,
                    "text_length": len(cleaned_text)
                }
                filename = f"{base_filename}.json"
                with open(filename, 'w', encoding='utf-8') as f:
                    json.dump(output_data, f, ensure_ascii=False, indent=2)
            else:
                filename = f"{base_filename}.txt"
                with open(filename, 'w', encoding='utf-8') as f:
                    f.write(f"URL: {url}\n")
                    f.write(f"Timestamp: {datetime.now().isoformat()}\n")
                    f.write(f"Text Length: {len(cleaned_text)} characters\n\n")
                    f.write("=== STRUCTURED DATA ===\n")
                    f.write(f"Title: {structured_data['title']}\n")
                    f.write(f"Meta Description: {structured_data['meta_description']}\n")
                    f.write(f"Meta Keywords: {structured_data['meta_keywords']}\n\n")
                    f.write("H1 Headings:\n")
                    for h in structured_data['headings']['h1']:
                        f.write(f"- {h}\n")
                    f.write("\nH2 Headings:\n")
                    for h in structured_data['headings']['h2']:
                        f.write(f"- {h}\n")
                    f.write("\n=== ALL TEXT ===\n")
                    f.write(cleaned_text)

            print(f"Data saved to {filename}")

        return cleaned_text

    except requests.RequestException as e:
        print(f"Error fetching page: {e}")
        return None
    except Exception as e:
        print(f"Error processing page: {e}")
        return None


def main():
    print("=== TopCV.vn Homepage Text Extractor ===")

    import argparse
    parser = argparse.ArgumentParser(description="Extract text from TopCV.vn homepage")
    parser.add_argument("--save", action="store_true", help="Save output to file")
    parser.add_argument("--format", choices=["text", "json"], default="text", help="Output format")
    parser.add_argument("--quiet", action="store_true", help="Suppress text output")

    args = parser.parse_args()

    text = extract_all_text_from_topcv(save_to_file=args.save, output_format=args.format)

    if text:
        if not args.quiet:
            print("\n=== Extracted Text (first 1000 characters) ===")
            print(text[:1000])
            if len(text) > 1000:
                print(f"\n... ({len(text) - 1000} more characters)")
        print(f"\nTotal text length: {len(text)} characters")
    else:
        print("Failed to extract text")
        sys.exit(1)


if __name__ == "__main__":
    main()