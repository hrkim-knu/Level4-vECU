import random

output_dir = "generated_tests"
total_tests = 4096
max_tests = 4096

# Create a list of all possible test cases
all_test_cases = list(range(max_tests))

# Randomly select the specified number of test cases
selected_test_cases = random.sample(all_test_cases, total_tests)

# Write the selected test cases to the yaml file
with open('adc_led_test.yaml', 'w') as yaml_file:
    for i in selected_test_cases:
        yaml_file.write(f'- {output_dir}/test_{i}.robot\n')
