<?php

namespace Drupal\sitecore\Form;

use Drupal\Core\Form\ConfigFormBase;
use Drupal\Core\Form\FormStateInterface;
use Drupal\file\Entity\File;
use Drupal\node\Entity\Node;
use Drupal\block\Entity\Block;

/**
 * Class SettingsForm.
 */
class SettingsForm extends ConfigFormBase {

  /**
   * {@inheritdoc}
   */
  protected function getEditableConfigNames() {
    return [
      'sitecore.settings',
    ];
  }

  /**
   * {@inheritdoc}
   */
  public function getFormId() {
    return 'settings_form';
  }

  /**
   * {@inheritdoc}
   */
  public function buildForm(array $form, FormStateInterface $form_state) {
    $form = array(
        '#attributes' => array('enctype' => 'multipart/form-data'),
    );
    $validators = array(
        'file_validate_extensions' => array('db'),
    );
    $form['sqlite_db_file'] = [
      '#type' => 'managed_file',
      '#title' => $this->t('Sitecore Database File'),
      '#upload_validators' => $validators,
      '#upload_location' => 'public://sitecore_dbs/',
    ];
    return parent::buildForm($form, $form_state);
  }

  /**
   * {@inheritdoc}
   */
  public function submitForm(array &$form, FormStateInterface $form_state) {

    $form_file = $form_state->getValue('sqlite_db_file', 0);

    if (isset($form_file[0]) && !empty($form_file[0])) {
        $file = File::load($form_file[0]);
        $file->setPermanent();
        $file->save();
        
        $absolute_path = \Drupal::service('file_system')->realpath($file->getFileUri());
        
        try {
            
            $pdo = NULL;
            $pdo = new \PDO("sqlite:" . $absolute_path);
            
            $stmt = $pdo->query("SELECT * FROM pages");
            $rows = $stmt->fetchAll(\PDO::FETCH_ASSOC);
            
            $pdo = NULL;

            /** THIS CREATES A BLOCK CONFIG **/
//             $block = Block::create(array(
//                 'id' => 'test_default_config',
//                 'theme' => 'bartik',
//                 'status' => TRUE,
//                 'region' => 'content',
//                 'plugin' => 'local_tasks_block',
//                 'settings' => [
//                     'id' => 'local_tasks_block',
//                     'label' => 'XXXYYYZZZ',
//                     'provider' => 'core',
//                     'label_display' => FALSE,
//                     'primary' => TRUE,
//                     'secondary' => TRUE
//                 ]
//             ));
//             $block->save();
            
            /*Block::create(array(
             'id' => 'test_id',
             'type' => 'basic',
             'label' => 'Test Label',
             'region' => 'content',
             'info' => $items['ItemName'],
             'plugin' => 'system_main_block',
             'body' => array(
             'value' => '<p>TEST BLOCK BODY VALUE</p>',
             'format' => 'basic_html'
             ),
             ))
             ->save();*/
            
            foreach($rows as $row){
                
                $new_row_str = '';
                $new_row_str = str_replace('"Teaser Image":"D:\drupal-media\media library\Feature\noimageavailable.png",', '', $row['Page']);
                $page = json_decode($new_row_str, TRUE);
                
                $items = json_decode($row['Items']); 
                
                echo '<pre>';
                var_dump($items);
                
                foreach($items as $item){
                    $blockEntityManager = \Drupal::service('entity.manager')
                        ->getStorage('block_content');
                    $block = $blockEntityManager->create(array(
                        'type' => 'basic'
                    ));
                    $block->info = $item->ItemName;
//                     $block->uuid = 'test_default_config';
                    $block->body->value = '...text of the block with allowed HTML tags...';
                    $block->body->format = 'full_html';
                    $block->save();
                }
                
                continue;
                
                $tags = array();
                foreach($page['Taxonomy Terms'] as $term){
                    
                    $tids = '';
                    $query = \Drupal::entityQuery('taxonomy_term');
                    $query->condition('vid', "sitecore_tags");
                    $query->condition('name', $term['DisplayName']);
                    $tids = $query->execute();
                    
                    $arr = array();
                    $arr = array_keys($tids);
                    
                    if(empty($tids)){
                        $new_term = \Drupal\taxonomy\Entity\Term::create([
                            'vid' => 'sitecore_tags',
                            'name' => $term['DisplayName'],
                        ]);
                        $new_term->save();
                        $tags[] = array('target_id' => $new_term->id());
                    } else {
                        $tags[] =  array('target_id' => $arr[0]);
                    }
                }
                
                // create the page
                Node::create(array(
                    'type' => 'sc_article',
                    'title' => $row['TemplateName'],
                    'body' => t('TEST BODY'),
                    'field_tags' => $tags,
                ))
                ->save();
                
            }
            
            die();
            
            $this->messenger()->addStatus($this->t('Congratulations, your Sitecore Database has been imported.'));
            
        } catch (\PDOException $e) {
            $this->messenger()->addError($this->t($e->getMessage()));
        }
        
    }

  }

}
