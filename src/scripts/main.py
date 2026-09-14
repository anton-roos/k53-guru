import json
import time
import requests

url = "https://www.getyourlearners.co.za/wp-admin/admin-ajax.php"

# Extract headers and cookies from your curl
headers = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/153.0.0.0 Safari/537.36",
    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
    "X-Requested-With": "XMLHttpRequest",
    "Referer": "https://www.getyourlearners.co.za/k53-learner-s-test/",
}

cookies = {
    # Paste your cookie values here
    "wordpress_logged_in_a2f73efc92a3973eee1c40b0021adea3": "bosratel...",
    "wordpress_sec_a2f73efc92a3973eee1c40b0021adea3": "bosratel...",
}

session = requests.Session()
session.headers.update(headers)
session.cookies.update(cookies)

questions_collected = {}

# Attempt bulk fetch first
payload = {
    "action": "get_questions",
    "options[limit]": "50",  # Test increasing this
    "options[order_by]": "random",
    "options[unique]": "false",
}

for iteration in range(25):  # Adjust loop count as needed
    response = session.post(url, data=payload)
    if response.status_code == 200:
        try:
            data = response.json()
            # If the response contains a list or dict of questions:
            # Parse and store by question_id to deduplicate automatically
            print(f"Batch {iteration + 1} retrieved successfully.")
        except Exception:
            # If response is HTML, parse the questions with BeautifulSoup
            print(f"Batch {iteration + 1} returned raw HTML markup.")
    else:
        print(f"Request failed with status {response.status_code}")
        break

    time.sleep(1)  # Rate limiting courtesy pause